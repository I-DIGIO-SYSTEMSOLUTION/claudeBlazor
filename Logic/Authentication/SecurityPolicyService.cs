namespace LogicCommon
{
    // パスワードポリシー画面の入力 (security_policies のパスワードポリシーと自動ログアウトの列)
    // record にしているのは、変更があったかの比較 (==) と複製 (with) を簡単にするため
    public sealed record PasswordPolicyInput
    {
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public bool RequireUppercase { get; set; }
        public bool RequireLowercase { get; set; }
        public bool RequireDigit { get; set; }
        public bool RequireSymbol { get; set; }
        public int MinCharTypes { get; set; }
        public bool DisallowLoginId { get; set; }
        public int HistoryCount { get; set; }
        public int ExpiryDays { get; set; }
        public int ExpiryWarningDays { get; set; }

        // 自動ログアウト
        public int IdleTimeoutMinutes { get; set; }
        public int TimeoutWarningSeconds { get; set; }

        // 読み込んだ時点の更新日時 (保存時に他の人の変更と重なっていないかの確認に使う)
        public DateTime UpdatedAt { get; set; }

        public static PasswordPolicyInput From(SecurityPolicy policy) => new()
        {
            MinLength = policy.PasswordMinLength,
            MaxLength = policy.PasswordMaxLength,
            RequireUppercase = policy.RequireUppercase,
            RequireLowercase = policy.RequireLowercase,
            RequireDigit = policy.RequireDigit,
            RequireSymbol = policy.RequireSymbol,
            MinCharTypes = policy.MinCharTypes,
            DisallowLoginId = policy.DisallowLoginId,
            HistoryCount = policy.PasswordHistoryCount,
            ExpiryDays = policy.PasswordExpiryDays,
            ExpiryWarningDays = policy.PasswordExpiryWarningDays,
            IdleTimeoutMinutes = policy.IdleTimeoutMinutes,
            TimeoutWarningSeconds = policy.TimeoutWarningSeconds,
            UpdatedAt = policy.UpdatedAt,
        };

        // 入力した値で置き換えたポリシー (説明文の表示と保存に使う)
        public SecurityPolicy ToPolicy() => new()
        {
            PasswordMinLength = MinLength,
            PasswordMaxLength = MaxLength,
            RequireUppercase = RequireUppercase,
            RequireLowercase = RequireLowercase,
            RequireDigit = RequireDigit,
            RequireSymbol = RequireSymbol,
            MinCharTypes = MinCharTypes,
            DisallowLoginId = DisallowLoginId,
            PasswordHistoryCount = HistoryCount,
            PasswordExpiryDays = ExpiryDays,
            PasswordExpiryWarningDays = ExpiryWarningDays,
            IdleTimeoutMinutes = IdleTimeoutMinutes,
            TimeoutWarningSeconds = TimeoutWarningSeconds,
        };

        public int RequiredTypeCount =>
            (RequireUppercase ? 1 : 0) + (RequireLowercase ? 1 : 0) + (RequireDigit ? 1 : 0) + (RequireSymbol ? 1 : 0);
    }

    // パスワードポリシーと自動ログアウトの時間の表示と変更 (管理者のパスワードポリシー画面)
    // 変更したポリシーは、これから設定するパスワード (作成・変更・リセット) に使われ、設定済みのパスワードはそのまま使える。
    // 有効期限は次のログインから、自動ログアウトの時間はログイン中のセッションにもすぐに (最大 10 秒で) 使われる
    public sealed class SecurityPolicyService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SecurityPolicyService));

        // 画面で入力できる上限 (DB の CHECK 制約に上限は無いが、現実的な値に抑える)
        public const int MaxPasswordLength = 1024;
        public const int MaxHistoryCount = 24;
        public const int MaxExpiryDays = 3650;
        // 自動ログアウトの時間の上限 (24 時間)
        public const int MaxIdleTimeoutMinutes = 1440;

        private readonly AuthRepository repository;

        public SecurityPolicyService(AuthRepository repository)
        {
            this.repository = repository;
        }

        // 画面に出す最新の値 (キャッシュを使わない)
        public async Task<PasswordPolicyInput> GetPasswordPolicyAsync() =>
            PasswordPolicyInput.From(await repository.GetSecurityPolicyAsync(useCache: false));

        public static List<string> Validate(PasswordPolicyInput input)
        {
            List<string> errors = new();
            if (input.MinLength < 1) errors.Add("最小文字数は 1 以上にしてください。");
            if (input.MaxLength > MaxPasswordLength) errors.Add($"最大文字数は {MaxPasswordLength} 以下にしてください。");
            if (input.MaxLength < input.MinLength) errors.Add("最大文字数は最小文字数以上にしてください。");
            if (input.MinCharTypes is < 0 or > 4) errors.Add("文字種の数は 0 から 4 の間にしてください。");
            // 必須にした文字種の数や、求める文字種の数より短いパスワードは作れないので、組み合わせとして止める
            int typesNeeded = Math.Max(input.MinCharTypes, input.RequiredTypeCount);
            if (input.MinLength < typesNeeded)
            {
                errors.Add($"最小文字数は、必要な文字種の数 ({typesNeeded}) 以上にしてください。");
            }
            if (input.HistoryCount is < 0 or > MaxHistoryCount) errors.Add($"再利用を禁止する世代数は 0 から {MaxHistoryCount} の間にしてください。");
            if (input.ExpiryDays is < 0 or > MaxExpiryDays) errors.Add($"有効期限は 0 から {MaxExpiryDays} 日の間にしてください。");
            if (input.ExpiryWarningDays < 0)
            {
                errors.Add("有効期限切れ警告表示日数は 0 以上にしてください。");
            }
            else if (input.ExpiryDays > 0 && input.ExpiryWarningDays >= input.ExpiryDays)
            {
                // 期限と同じ日数以上にすると、パスワードを変えた直後から毎回知らせてしまう
                errors.Add("有効期限切れ警告表示日数は、有効期限の日数より小さくしてください。");
            }

            if (input.IdleTimeoutMinutes is < 1 or > MaxIdleTimeoutMinutes)
            {
                errors.Add($"自動ログアウトまでの時間は 1 から {MaxIdleTimeoutMinutes} 分の間にしてください。");
            }
            else if (input.TimeoutWarningSeconds < 0 || input.TimeoutWarningSeconds >= input.IdleTimeoutMinutes * 60)
            {
                // DB の CHECK 制約 (ck_security_policies_timeout_warning) と同じ条件
                errors.Add($"警告を出す秒数は 0 以上、自動ログアウトまでの時間 ({input.IdleTimeoutMinutes * 60} 秒) より短くしてください。");
            }
            return errors;
        }

        public async Task<AccountResult> UpdatePasswordPolicyAsync(PasswordPolicyInput input, long changedBy)
        {
            List<string> errors = Validate(input);
            if (errors.Count > 0)
            {
                return AccountResult.Fail(errors);
            }

            SecurityPolicy before = await repository.GetSecurityPolicyAsync(useCache: false);
            if (!await repository.UpdatePasswordPolicyAsync(input.ToPolicy(), input.UpdatedAt, DateTime.UtcNow))
            {
                return AccountResult.Fail("画面を開いた後に、他の人がパスワードポリシーを変更しました。画面を開き直してから変更してください。");
            }

            // 監査のため、変更した人と変更前後の値を残す
            logger.Info($"パスワードポリシーを変更しました (変更者のユーザーID {changedBy})。" +
                $"変更前: {PasswordPolicy.Describe(before)}、有効期限 {before.PasswordExpiryDays} 日 ({before.PasswordExpiryWarningDays} 日前から通知)、" +
                $"自動ログアウト {before.IdleTimeoutMinutes} 分 (警告 {before.TimeoutWarningSeconds} 秒前) / " +
                $"変更後: {PasswordPolicy.Describe(input.ToPolicy())}、有効期限 {input.ExpiryDays} 日 ({input.ExpiryWarningDays} 日前から通知)、" +
                $"自動ログアウト {input.IdleTimeoutMinutes} 分 (警告 {input.TimeoutWarningSeconds} 秒前)");
            return AccountResult.Ok();
        }
    }
}
