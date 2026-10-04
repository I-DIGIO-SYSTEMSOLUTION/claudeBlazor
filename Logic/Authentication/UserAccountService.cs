using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogicCommon
{
    // 処理結果。失敗した場合は画面に出すメッセージを持つ
    public sealed record AccountResult(bool Succeeded, IReadOnlyList<string> Errors)
    {
        public static AccountResult Ok() => new(true, []);
        public static AccountResult Fail(params string[] errors) => new(false, errors);
        public static AccountResult Fail(IEnumerable<string> errors) => new(false, errors.ToList());
    }

    // ユーザー作成画面の入力
    public sealed class NewUserInput
    {
        public string LoginId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string PasswordConfirm { get; set; } = "";
        public IReadOnlyCollection<string> RoleCodes { get; set; } = [];
    }

    // ユーザーの作成とパスワードの変更
    public sealed class UserAccountService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(UserAccountService));

        // 一意制約違反 (同時に同じログイン ID・メールアドレスで登録された場合)
        // PostgreSQL は SQLSTATE 23505、SQL Server はエラー番号 2627 (一意制約) / 2601 (一意インデックス)
        private const string UniqueViolationSqlState = "23505";
        private static bool IsUniqueViolation(Exception ex) =>
            ex is Npgsql.PostgresException { SqlState: UniqueViolationSqlState } ||
            ex is Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 };

        private readonly AuthRepository repository;
        private readonly PasswordPolicy passwordPolicy;

        public UserAccountService(AuthRepository repository, PasswordPolicy passwordPolicy)
        {
            this.repository = repository;
            this.passwordPolicy = passwordPolicy;
        }

        public Task<SecurityPolicy> GetSecurityPolicyAsync() => repository.GetSecurityPolicyAsync();

        public async Task<List<User>> GetUsersAsync()
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await repository.GetUsersAsync(db);
        }

        public async Task<List<Role>> GetRolesAsync()
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await repository.GetRolesAsync(db);
        }

        // ユーザーが 1 人もいないとき (最初の管理者を登録する必要があるとき) は true
        public async Task<bool> IsSetupRequiredAsync()
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            return await repository.CountUsersAsync(db) == 0;
        }

        // 最初の管理者を登録する (ユーザーが 0 人のときだけ)。ADMIN ロールが無ければ作る
        public async Task<AccountResult> CreateFirstAdminAsync(NewUserInput input)
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();

            if (await repository.CountUsersAsync(db) > 0)
            {
                return AccountResult.Fail("既にユーザーが登録されています。ログイン画面からログインしてください。");
            }

            DateTime now = DateTime.UtcNow;
            await repository.InsertRoleIfMissingAsync(db, AuthConstants.AdminRole, "管理者", now);
            input.RoleCodes = [AuthConstants.AdminRole];

            AccountResult result = await CreateUserCoreAsync(db, input, null, now);
            if (result.Succeeded)
            {
                await tx.CommitAsync();
                logger.Info($"最初の管理者を登録しました (ログインID {input.LoginId})。");
            }
            return result;
        }

        // 管理者がユーザーを作成する
        public async Task<AccountResult> CreateUserAsync(NewUserInput input, long createdBy)
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();

            AccountResult result = await CreateUserCoreAsync(db, input, createdBy, DateTime.UtcNow);
            if (result.Succeeded)
            {
                await tx.CommitAsync();
                logger.Info($"ユーザーを作成しました (ログインID {input.LoginId}, 作成者のユーザーID {createdBy})。");
            }
            return result;
        }

        private async Task<AccountResult> CreateUserCoreAsync(DbContextAuth db, NewUserInput input, long? createdBy, DateTime now)
        {
            input.LoginId = input.LoginId.Trim();
            input.DisplayName = input.DisplayName.Trim();
            input.Email = input.Email.Trim();

            List<string> errors = ValidateUserFields(input);
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            errors.AddRange(passwordPolicy.Validate(policy, input.Password, input.LoginId));
            if (input.Password != input.PasswordConfirm)
            {
                errors.Add("パスワードと確認用のパスワードが一致しません。");
            }
            if (errors.Count == 0)
            {
                if (await repository.LoginIdExistsAsync(db, input.LoginId)) errors.Add("このログイン ID は既に使われています。");
                if (await repository.EmailExistsAsync(db, input.Email)) errors.Add("このメールアドレスは既に使われています。");
            }

            // 外部キーが無いので、ロールが存在することをここで確認する
            List<long> roleIds = new();
            if (errors.Count == 0 && input.RoleCodes.Count > 0)
            {
                roleIds = await repository.GetRoleIdsAsync(db, input.RoleCodes.Distinct().ToList());
                if (roleIds.Count != input.RoleCodes.Distinct().Count())
                {
                    errors.Add("存在しないロールが指定されています。");
                }
            }
            if (errors.Count > 0)
            {
                return AccountResult.Fail(errors);
            }

            string hash = passwordPolicy.Hash(input.Password);
            string historyJson = PasswordPolicy.PrependHistory([],
                new PasswordHistoryEntry(hash, now, createdBy, PasswordChangeReasons.Initial));

            try
            {
                long userId = await repository.InsertUserAsync(db, input.LoginId, hash, historyJson, input.DisplayName, input.Email, now);
                foreach (long roleId in roleIds)
                {
                    await repository.InsertUserRoleAsync(db, userId, roleId, now);
                }
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                return AccountResult.Fail("ログイン ID またはメールアドレスが既に使われています。");
            }
            return AccountResult.Ok();
        }

        private static List<string> ValidateUserFields(NewUserInput input)
        {
            List<string> errors = new();
            if (input.LoginId.Length == 0) errors.Add("ログイン ID を入力してください。");
            else if (input.LoginId.Length > 256) errors.Add("ログイン ID は 256 文字以下にしてください。");
            else if (input.LoginId.Any(char.IsWhiteSpace)) errors.Add("ログイン ID に空白は使えません。");

            if (input.DisplayName.Length == 0) errors.Add("表示名を入力してください。");
            else if (input.DisplayName.Length > 100) errors.Add("表示名は 100 文字以下にしてください。");

            if (input.Email.Length == 0) errors.Add("メールアドレスを入力してください。");
            else if (input.Email.Length > 256 || !IsValidEmail(input.Email)) errors.Add("メールアドレスの形式が正しくありません。");

            return errors;
        }

        public static bool IsValidEmail(string email) =>
            MailAddress.TryCreate(email, out MailAddress? address) && address.Address == email;

        // 本人がパスワードを変更する。操作中のセッションは残し、他の端末のセッションは終了する
        public async Task<AccountResult> ChangePasswordAsync(long userId, string currentPassword, string newPassword,
            string newPasswordConfirm, Guid currentSessionId)
        {
            await using DbContextAuth db = await repository.CreateDbContextAsync();
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync();

            User? user = await repository.GetUserByIdAsync(db, userId);
            if (user == null || !user.IsActive)
            {
                return AccountResult.Fail("ユーザーが見つかりません。");
            }
            if (!passwordPolicy.Verify(user.PasswordHash, currentPassword))
            {
                return AccountResult.Fail("現在のパスワードが正しくありません。");
            }
            if (newPassword != newPasswordConfirm)
            {
                return AccountResult.Fail("新しいパスワードと確認用のパスワードが一致しません。");
            }

            (List<string> errors, List<PasswordHistoryEntry> history) = await ValidateNewPasswordAsync(db, user, newPassword);
            if (errors.Count > 0)
            {
                return AccountResult.Fail(errors);
            }

            AccountResult result = await ApplyNewPasswordAsync(db, user, newPassword, history,
                PasswordChangeReasons.UserChange, userId, currentSessionId, DateTime.UtcNow);
            if (result.Succeeded)
            {
                await tx.CommitAsync();
            }
            return result;
        }

        // 新しいパスワードがポリシーを満たし、直近のパスワードと重複していないか (DB は更新しない)
        internal async Task<(List<string> Errors, List<PasswordHistoryEntry> History)> ValidateNewPasswordAsync(
            DbContextAuth db, User user, string newPassword)
        {
            SecurityPolicy policy = await repository.GetSecurityPolicyAsync();
            List<string> errors = passwordPolicy.Validate(policy, newPassword, user.LoginId);
            List<PasswordHistoryEntry> history = PasswordPolicy.ParseHistory(await repository.GetPasswordHistoryJsonAsync(db, user.Id));
            if (errors.Count == 0 && passwordPolicy.CheckHistory(policy, history, user.PasswordHash, newPassword) is string historyError)
            {
                errors.Add(historyError);
            }
            return (errors, history);
        }

        // パスワードを更新し、履歴の先頭に追加し、セッションを終了する (keepSessionId のセッションだけは残す)
        // 呼び出し側のトランザクションの中で使う
        internal async Task<AccountResult> ApplyNewPasswordAsync(DbContextAuth db, User user, string newPassword,
            IReadOnlyList<PasswordHistoryEntry> history, string reason, long? changedBy, Guid? keepSessionId, DateTime now)
        {
            string hash = passwordPolicy.Hash(newPassword);
            string historyJson = PasswordPolicy.PrependHistory(history, new PasswordHistoryEntry(hash, now, changedBy, reason));

            if (!await repository.UpdatePasswordAsync(db, user.Id, hash, historyJson, Guid.NewGuid().ToString(), user.UpdatedAt, now))
            {
                return AccountResult.Fail("他の操作と重なったため変更できませんでした。もう一度お試しください。");
            }
            await repository.EndSessionsByUserAsync(db, user.Id, SessionEndReasons.PasswordChanged, keepSessionId, now);
            logger.Info($"パスワードを変更しました (ユーザーID {user.Id}, 理由 {reason})。");
            return AccountResult.Ok();
        }
    }
}
