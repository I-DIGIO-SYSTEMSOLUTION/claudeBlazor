#nullable disable

using Microsoft.EntityFrameworkCore;

namespace LogicCommon
{
    public class DbContextImageAnnotationComment : DbContext
    {
        public DbSet<ANNOTATION_COMMENT_TBL> ANNOTATION_COMMENT_TBL { get; set; }
        public DbSet<ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL> ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL { get; set; }
        public DbContextImageAnnotationComment(DbContextOptions<DbContextImageAnnotationComment> options) : base(options) { }        
    }
}