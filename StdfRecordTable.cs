using System;
using System.Collections.Generic;

namespace StdfViewer
{
    public enum StdfType
    {
        U1, U2, U4,
        I1, I2, I4,
        R4, R8,
        C1, Cn, Cf,
        B1, Bn, Dn,
        N1, Vn
    }

    public readonly struct StdfField
    {
        public StdfField(string name, StdfType type, string countField = null, bool isTime = false)
        {
            Name = name;
            Type = type;
            CountField = countField;
            IsTime = isTime;
        }

        public string Name { get; }
        public StdfType Type { get; }

        // 非 null 表示重复字段 kxTYPE，个数取自同记录内这个字段的值
        public string CountField { get; }

        public bool IsTime { get; }

        public string TypeText
        {
            get
            {
                string baseName = BaseText(Type);
                return CountField == null ? baseName : "k" + baseName;
            }
        }

        public static string BaseText(StdfType t)
        {
            switch (t)
            {
                case StdfType.U1: return "U*1";
                case StdfType.U2: return "U*2";
                case StdfType.U4: return "U*4";
                case StdfType.I1: return "I*1";
                case StdfType.I2: return "I*2";
                case StdfType.I4: return "I*4";
                case StdfType.R4: return "R*4";
                case StdfType.R8: return "R*8";
                case StdfType.C1: return "C*1";
                case StdfType.Cn: return "C*n";
                case StdfType.Cf: return "C*f";
                case StdfType.B1: return "B*1";
                case StdfType.Bn: return "B*n";
                case StdfType.Dn: return "D*n";
                case StdfType.N1: return "N*1";
                default: return "V*n";
            }
        }

        public static int FixedSize(StdfType t)
        {
            switch (t)
            {
                case StdfType.U1:
                case StdfType.I1:
                case StdfType.C1:
                case StdfType.B1: return 1;
                case StdfType.U2:
                case StdfType.I2: return 2;
                case StdfType.U4:
                case StdfType.I4:
                case StdfType.R4: return 4;
                case StdfType.R8: return 8;
                default: return 0;
            }
        }
    }

    public sealed class StdfRecordDef
    {
        public StdfRecordDef(string name, byte typ, byte sub, StdfField[] fields)
        {
            Name = name;
            Typ = typ;
            Sub = sub;
            Fields = fields;
        }

        public string Name { get; }
        public byte Typ { get; }
        public byte Sub { get; }
        public StdfField[] Fields { get; }
    }

    public static class StdfRecordTable
    {
        // 记录号按本工程采用的 STDF V4 表定义，与官方 STDF 编号不同：FAR=0,10 / PIR=5,10 / RDR=1,70
        private static readonly StdfRecordDef[] Defs =
        {
            new StdfRecordDef("FAR", 0, 10, new[]
            {
                F("CPU_TYPE", StdfType.U1),
                F("STDF_VER", StdfType.U1),
            }),
            new StdfRecordDef("ATR", 0, 20, new[]
            {
                F("MOD_TIM", StdfType.U4, null, true),
                F("CMD_LINE", StdfType.Cn),
            }),
            new StdfRecordDef("MIR", 1, 10, new[]
            {
                F("SETUP_T", StdfType.U4, null, true),
                F("START_T", StdfType.U4, null, true),
                F("STAT_NUM", StdfType.U1),
                F("MODE_COD", StdfType.C1),
                F("RTST_COD", StdfType.C1),
                F("PROT_COD", StdfType.C1),
                F("BURN_TIM", StdfType.U2),
                F("CMOD_COD", StdfType.C1),
                F("LOT_ID", StdfType.Cn),
                F("PART_TYP", StdfType.Cn),
                F("NODE_NAM", StdfType.Cn),
                F("TSTR_TYP", StdfType.Cn),
                F("JOB_NAM", StdfType.Cn),
                F("JOB_REV", StdfType.Cn),
                F("SBLOT_ID", StdfType.Cn),
                F("OPER_NAM", StdfType.Cn),
                F("EXEC_TYP", StdfType.Cn),
                F("EXEC_VER", StdfType.Cn),
                F("TEST_COD", StdfType.Cn),
                F("TST_TEMP", StdfType.Cn),
                F("USER_TXT", StdfType.Cn),
                F("AUX_FILE", StdfType.Cn),
                F("PKG_TYP", StdfType.Cn),
                F("FAMLY_ID", StdfType.Cn),
                F("DATE_COD", StdfType.Cn),
                F("FACIL_ID", StdfType.Cn),
                F("FLOOR_ID", StdfType.Cn),
                F("PROC_ID", StdfType.Cn),
                F("OPER_FRQ", StdfType.Cn),
                F("SPEC_NAM", StdfType.Cn),
                F("SPEC_VER", StdfType.Cn),
                F("FLOW_ID", StdfType.Cn),
                F("SETUP_ID", StdfType.Cn),
                F("DSGN_REV", StdfType.Cn),
                F("ENG_ID", StdfType.Cn),
                F("ROM_COD", StdfType.Cn),
                F("SERL_NUM", StdfType.Cn),
                F("SUPR_NAM", StdfType.Cn),
            }),
            new StdfRecordDef("MRR", 1, 20, new[]
            {
                F("FINISH_T", StdfType.U4, null, true),
                F("DISP_COD", StdfType.C1),
                F("USR_DESC", StdfType.Cn),
                F("EXC_DESC", StdfType.Cn),
            }),
            new StdfRecordDef("PCR", 1, 30, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("PART_CNT", StdfType.U4),
                F("RTST_CNT", StdfType.U4),
                F("ABRT_CNT", StdfType.U4),
                F("GOOD_CNT", StdfType.U4),
                F("FUNC_CNT", StdfType.U4),
            }),
            new StdfRecordDef("HBR", 1, 40, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("HBIN_NUM", StdfType.U2),
                F("HBIN_CNT", StdfType.U4),
                F("HBIN_PF", StdfType.C1),
                F("HBIN_NAM", StdfType.Cn),
            }),
            new StdfRecordDef("SBR", 1, 50, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("SBIN_NUM", StdfType.U2),
                F("SBIN_CNT", StdfType.U4),
                F("SBIN_PF", StdfType.C1),
                F("SBIN_NAM", StdfType.Cn),
            }),
            new StdfRecordDef("PMR", 1, 60, new[]
            {
                F("PMR_INDX", StdfType.U2),
                F("CHAN_TYP", StdfType.U2),
                F("CHAN_NAM", StdfType.Cn),
                F("PHY_NAM", StdfType.Cn),
                F("LOG_NAM", StdfType.Cn),
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
            }),
            new StdfRecordDef("PGR", 1, 62, new[]
            {
                F("GRP_INDX", StdfType.U2),
                F("GRP_NAM", StdfType.Cn),
                F("INDX_CNT", StdfType.U2),
                F("PMR_INDX", StdfType.U2, "INDX_CNT"),
            }),
            new StdfRecordDef("PLR", 1, 63, new[]
            {
                F("GRP_CNT", StdfType.U2),
                F("GRP_INDX", StdfType.U2, "GRP_CNT"),
                F("GRP_MODE", StdfType.U2, "GRP_CNT"),
                F("GRP_RADX", StdfType.U1, "GRP_CNT"),
                F("PGM_CHAR", StdfType.Cn, "GRP_CNT"),
                F("RTN_CHAR", StdfType.Cn, "GRP_CNT"),
                F("PGM_CHAL", StdfType.Cn, "GRP_CNT"),
                F("RTN_CHAL", StdfType.Cn, "GRP_CNT"),
            }),
            new StdfRecordDef("RDR", 1, 70, new[]
            {
                F("NUM_BINS", StdfType.U2),
                F("RTST_BIN", StdfType.U2, "NUM_BINS"),
            }),
            new StdfRecordDef("SDR", 1, 80, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_GRP", StdfType.U1),
                F("SITE_CNT", StdfType.U1),
                F("SITE_NUM", StdfType.U1, "SITE_CNT"),
                F("HAND_TYP", StdfType.Cn),
                F("HAND_ID", StdfType.Cn),
                F("CARD_TYP", StdfType.Cn),
                F("CARD_ID", StdfType.Cn),
                F("LOAD_TYP", StdfType.Cn),
                F("LOAD_ID", StdfType.Cn),
                F("DIB_TYP", StdfType.Cn),
                F("DIB_ID", StdfType.Cn),
                F("CABL_TYP", StdfType.Cn),
                F("CABL_ID", StdfType.Cn),
                F("CONT_TYP", StdfType.Cn),
                F("CONT_ID", StdfType.Cn),
                F("LASR_TYP", StdfType.Cn),
                F("LASR_ID", StdfType.Cn),
                F("EXTR_TYP", StdfType.Cn),
                F("EXTR_ID", StdfType.Cn),
            }),
            new StdfRecordDef("WIR", 2, 10, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_GRP", StdfType.U1),
                F("START_T", StdfType.U4, null, true),
                F("WAFER_ID", StdfType.Cn),
            }),
            new StdfRecordDef("WRR", 2, 20, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_GRP", StdfType.U1),
                F("FINISH_T", StdfType.U4, null, true),
                F("PART_CNT", StdfType.U4),
                F("RTST_CNT", StdfType.U4),
                F("ABRT_CNT", StdfType.U4),
                F("GOOD_CNT", StdfType.U4),
                F("FUNC_CNT", StdfType.U4),
                F("WAFER_ID", StdfType.Cn),
                F("FABWF_ID", StdfType.Cn),
                F("FRAME_ID", StdfType.Cn),
                F("MASK_ID", StdfType.Cn),
                F("USR_DESC", StdfType.Cn),
                F("EXC_DESC", StdfType.Cn),
            }),
            new StdfRecordDef("WCR", 2, 30, new[]
            {
                F("WAFR_SIZ", StdfType.R4),
                F("DIE_HT", StdfType.R4),
                F("DIE_WID", StdfType.R4),
                F("WF_UNITS", StdfType.U1),
                F("WF_FLAT", StdfType.C1),
                F("CENTER_X", StdfType.I2),
                F("CENTER_Y", StdfType.I2),
                F("POS_X", StdfType.C1),
                F("POS_Y", StdfType.C1),
            }),
            new StdfRecordDef("PIR", 5, 10, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
            }),
            new StdfRecordDef("PRR", 5, 20, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("PART_FLG", StdfType.B1),
                F("NUM_TEST", StdfType.U2),
                F("HARD_BIN", StdfType.U2),
                F("SOFT_BIN", StdfType.U2),
                F("X_COORD", StdfType.I2),
                F("Y_COORD", StdfType.I2),
                F("TEST_T", StdfType.U4),
                F("PART_ID", StdfType.Cn),
                F("PART_TXT", StdfType.Cn),
                F("PART_FIX", StdfType.Bn),
            }),
            new StdfRecordDef("TSR", 10, 30, new[]
            {
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("TEST_TYP", StdfType.C1),
                F("TEST_NUM", StdfType.U4),
                F("EXEC_CNT", StdfType.U4),
                F("FAIL_CNT", StdfType.U4),
                F("ALRM_CNT", StdfType.U4),
                F("TEST_NAM", StdfType.Cn),
                F("SEQ_NAME", StdfType.Cn),
                F("TEST_LBL", StdfType.Cn),
                F("OPT_FLAG", StdfType.B1),
                F("TEST_TIM", StdfType.R4),
                F("TEST_MIN", StdfType.R4),
                F("TEST_MAX", StdfType.R4),
                F("TST_SUMS", StdfType.R4),
                F("TST_SQRS", StdfType.R4),
            }),
            new StdfRecordDef("PTR", 15, 10, new[]
            {
                F("TEST_NUM", StdfType.U4),
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("TEST_FLG", StdfType.B1),
                F("PARM_FLG", StdfType.B1),
                F("RESULT", StdfType.R4),
                F("TEST_TXT", StdfType.Cn),
                F("ALARM_ID", StdfType.Cn),
                F("OPT_FLAG", StdfType.B1),
                F("RES_SCAL", StdfType.I1),
                F("LLM_SCAL", StdfType.I1),
                F("HLM_SCAL", StdfType.I1),
                F("LO_LIMIT", StdfType.R4),
                F("HI_LIMIT", StdfType.R4),
                F("UNITS", StdfType.Cn),
                F("C_RESFMT", StdfType.Cn),
                F("C_LLMFMT", StdfType.Cn),
                F("C_HLMFMT", StdfType.Cn),
                F("LO_SPEC", StdfType.R4),
                F("HI_SPEC", StdfType.R4),
            }),
            // MPR / FTR 在本地样本文件里没有出现，字段表按规范表照录，未经字节回归验证
            new StdfRecordDef("MPR", 15, 15, new[]
            {
                F("TEST_NUM", StdfType.U4),
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("TEST_FLG", StdfType.B1),
                F("PARM_FLG", StdfType.B1),
                F("RTN_ICNT", StdfType.U2),
                F("RSLT_CNT", StdfType.U2),
                F("RTN_RSLT", StdfType.R4, "RSLT_CNT"),
                F("TEST_TXT", StdfType.Cn),
                F("ALARM_ID", StdfType.Cn),
                F("OPT_FLAG", StdfType.B1),
                F("RES_SCAL", StdfType.I1),
                F("LLM_SCAL", StdfType.I1),
                F("HLM_SCAL", StdfType.I1),
                F("LO_LIMIT", StdfType.R4),
                F("HI_LIMIT", StdfType.R4),
                F("START_IN", StdfType.R4),
                F("INCR_IN", StdfType.R4),
                F("UNITS", StdfType.Cn),
                F("UNITS_IN", StdfType.Cn),
                F("C_RESFMT", StdfType.Cn),
                F("C_LLMFMT", StdfType.Cn),
                F("C_HLMFMT", StdfType.Cn),
                F("LO_SPEC", StdfType.R4),
                F("HI_SPEC", StdfType.R4),
            }),
            new StdfRecordDef("FTR", 15, 20, new[]
            {
                F("TEST_NUM", StdfType.U4),
                F("HEAD_NUM", StdfType.U1),
                F("SITE_NUM", StdfType.U1),
                F("TEST_FLG", StdfType.B1),
                F("OPT_FLAG", StdfType.B1),
                F("CYCL_CNT", StdfType.U4),
                F("REL_VADR", StdfType.U4),
                F("REPT_CNT", StdfType.U4),
                F("NUM_FAIL", StdfType.U4),
                F("XFAIL_AD", StdfType.I4),
                F("YFAIL_AD", StdfType.I4),
                F("VECT_OFF", StdfType.I2),
                F("RTN_ICNT", StdfType.U2),
                F("PGM_ICNT", StdfType.U2),
                F("PGM_INDX", StdfType.U2, "PGM_ICNT"),
                F("PGM_STAT", StdfType.N1, "PGM_ICNT"),
                F("FAIL_PIN", StdfType.Dn),
                F("VECT_NAM", StdfType.Cn),
                F("TIME_SET", StdfType.Cn),
                F("OP_CODE", StdfType.Cn),
                F("TEST_TXT", StdfType.Cn),
                F("ALARM_ID", StdfType.Cn),
                F("PROG_TXT", StdfType.Cn),
                F("RSLT_TXT", StdfType.Cn),
                F("PATG_NUM", StdfType.U1),
                F("SPIN_MAP", StdfType.Dn),
            }),
            new StdfRecordDef("BPS", 20, 10, new[]
            {
                F("SEQ_NAME", StdfType.Cn),
            }),
            new StdfRecordDef("EPS", 20, 20, new StdfField[0]),
            new StdfRecordDef("GDR", 50, 10, new[]
            {
                F("FLD_CNT", StdfType.U2),
                F("GEN_DATA", StdfType.Vn, "FLD_CNT"),
            }),
            new StdfRecordDef("DTR", 50, 30, new[]
            {
                F("TEXT_DAT", StdfType.Cn),
            }),
        };

        private static readonly Dictionary<int, StdfRecordDef> ByKey = BuildIndex(Defs);

        public static StdfRecordDef Find(byte typ, byte sub)
        {
            StdfRecordDef def;
            return ByKey.TryGetValue(Key(typ, sub), out def) ? def : null;
        }

        public static string DisplayName(byte typ, byte sub)
        {
            StdfRecordDef def = Find(typ, sub);
            return def != null ? def.Name : typ + "," + sub;
        }

        private static Dictionary<int, StdfRecordDef> BuildIndex(StdfRecordDef[] defs)
        {
            var map = new Dictionary<int, StdfRecordDef>();
            foreach (StdfRecordDef d in defs)
                map.Add(Key(d.Typ, d.Sub), d);
            return map;
        }

        private static int Key(byte typ, byte sub)
        {
            return (typ << 8) | sub;
        }

        private static StdfField F(string name, StdfType type, string countField = null, bool isTime = false)
        {
            return new StdfField(name, type, countField, isTime);
        }
    }
}
