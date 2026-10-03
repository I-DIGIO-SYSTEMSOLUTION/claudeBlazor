#nullable disable

using Microsoft.EntityFrameworkCore;
using System;

namespace LogicCommon
{
    // DataGridに表示するデータ
    [PrimaryKey(nameof(JOB_NAME), nameof(BATCH_NAME), nameof(RECORD_NO), nameof(FLD_NO))]
    public class ANNOTATION_COMMENT_TBL
    {
        public ANNOTATION_COMMENT_TBL() { }

        // DeepCopy用
        public ANNOTATION_COMMENT_TBL(ANNOTATION_COMMENT_TBL ele)
        {
            JOB_NAME = ele.JOB_NAME;
            BATCH_NAME = ele.BATCH_NAME;
            RECORD_NO = ele.RECORD_NO;
            FLD_NO = ele.FLD_NO;
            ANNOTATION_COMMENT = ele.ANNOTATION_COMMENT;
            IMAGE_FILE_PATH = ele.IMAGE_FILE_PATH;
            UPDATE_BY = ele.UPDATE_BY;
            UPDATE_AT  = ele.UPDATE_AT;
        }

        public string JOB_NAME { get; set; }
        public string BATCH_NAME { get; set; }
        public string RECORD_NO { get; set; }
        public string FLD_NO { get; set; }
        public string ANNOTATION_COMMENT { get; set; }
        public string IMAGE_FILE_PATH { get; set; }
        public string UPDATE_BY { get; set; }
        public DateTime UPDATE_AT { get; set; }
    }
}
