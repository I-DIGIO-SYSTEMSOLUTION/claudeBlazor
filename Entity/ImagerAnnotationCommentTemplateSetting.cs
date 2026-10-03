#nullable disable

using Microsoft.EntityFrameworkCore;
using System;

namespace LogicCommon
{
    // DataGridに表示するデータ
    [PrimaryKey(nameof(JOB_NAME), nameof(PROCESS_ORDER))]
    public class ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL
    {
        public string JOB_NAME { get; set; }
        public int PROCESS_ORDER { get; set; }
        public string BEFORE_ANNOTATION_COMMENT { get; set; }
        public string AFTER_ANNOTATION_COMMENT { get; set; }
        public string UPDATE_BY { get; set; }
        public DateTime UPDATE_AT { get; set; }

        public ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL(ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL ele)
        {
            JOB_NAME = ele.JOB_NAME;
            PROCESS_ORDER = ele.PROCESS_ORDER;
            BEFORE_ANNOTATION_COMMENT = ele.BEFORE_ANNOTATION_COMMENT;
            AFTER_ANNOTATION_COMMENT = ele.AFTER_ANNOTATION_COMMENT;
            UPDATE_AT = ele.UPDATE_AT;
            UPDATE_BY = ele.UPDATE_BY;
        }

        public ANNOTATION_COMMENT_TEMPLATE_SETTING_TBL() { }

    }
}
