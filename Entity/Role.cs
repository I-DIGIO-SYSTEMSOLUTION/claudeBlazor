#nullable disable

using System.ComponentModel.DataAnnotations.Schema;

namespace LogicCommon
{
    // roles テーブル
    [Table("roles")]
    public class Role
    {
        [Column("id")]
        public long Id { get; set; }

        // [Authorize(Roles = ...)] で使うコード (例: ADMIN)
        [Column("code")]
        public string Code { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
