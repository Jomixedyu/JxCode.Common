﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;

namespace JxCode.Common
{
    public class ArgumentCommand : IEnumerable<string>
    {
        public string RecordName { get; private set; }
        public List<string> Args { get; private set; }
        public int ArgCount { get => Args.Count; }

        public ArgumentCommand(string record, List<string> args)
        {
            this.RecordName = record;
            this.Args = args;
        }
        public string GetFirstArg()
        {
            return Args[0];
        }
        public IEnumerator<string> GetEnumerator()
        {
            return Args.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Args.GetEnumerator();
        }
    }

    public class CheckInvalidResult
    {
        public bool IsSuccess { get; set; }
        public string Infomation { get; set; }

        public CheckInvalidResult(bool isSuccess, string infomation)
        {
            this.IsSuccess = isSuccess;
            this.Infomation = infomation;
        }
    }

    public class ArgumentConfig
    {
        public string RecordName { get; set; }
        public int ArgCount { get; set; }
        public bool IsRequired { get; set; }
        public Func<List<string>, CheckInvalidResult> CheckInvalidHandler;

        public ArgumentConfig(
            string record,
            int argCount,
            bool isRequired,
            Func<List<string>, CheckInvalidResult> checkInvalidHandler)
        {
            this.RecordName = record;
            this.ArgCount = argCount;
            this.IsRequired = isRequired;
            this.CheckInvalidHandler = checkInvalidHandler;
        }
        public override string ToString()
        {
            return this.RecordName;
        }
    }
    public class ArgumentConfigMessage : ArgumentConfig
    {
        public ArgumentConfigMessage(string record = "-help")
            : base(record, 0, false, null)
        {
        }
    }
    public class ArgumentConfigFile : ArgumentConfig
    {
        private bool hasExists;
        private string[] extensions;
        private bool HasExt(string filename)
        {
            string ext = Path.GetExtension(filename);
            return Array.IndexOf(extensions, ext) != -1;
        }
        private CheckInvalidResult CheckFileInvalid(List<string> str)
        {
            foreach (var filename in str)
            {
                if (hasExists && !File.Exists(filename))
                {
                    return new CheckInvalidResult(false, "not exists: " + filename);
                }
                if (!HasExt(filename))
                {
                    return new CheckInvalidResult(false, "format error: " + filename);
                }
            }
            return new CheckInvalidResult(true, null);
        }
        public ArgumentConfigFile(
            string record,
            int argCount = 1,
            bool isRequired = true,
            bool hasExists = true,
            string[] extensions = null
            ) : base(record, argCount, isRequired, null)
        {
            this.hasExists = hasExists;
            this.extensions = extensions;
            base.CheckInvalidHandler = this.CheckFileInvalid;
        }

        public static ArgumentConfigFile InputOnce(string record = "-i", string ext = null)
        {
            string[] extensions = ext == null ? null : new string[] { ext };
            return new ArgumentConfigFile(record, 1, true, true, extensions);
        }
        public static ArgumentConfigFile OutputOnce(string record = "-o", string ext = null)
        {
            string[] extensions = ext == null ? null : new string[] { ext };
            return new ArgumentConfigFile(record, 1, true, false, extensions);
        }
    }
    public class ArgumentConfigDirectory : ArgumentConfig
    {
        private static CheckInvalidResult CheckInvalidDir(List<string> str)
        {
            return default;
        }
        public ArgumentConfigDirectory(
            string record,
            int argCount = 1,
            bool isRequired = true
            ) : base(record, argCount, isRequired, CheckInvalidDir)
        {

        }
    }

    public class ArgumentParserException : ApplicationException
    {
        public ArgumentParserException(string msg) : base(msg) { }
    }
    public class ArgumentParserNotFindCommandException : ArgumentParserException
    {
        public ArgumentParserNotFindCommandException(string msg) : base(msg) { }
    }
    public class ArgumentParserInvalidArgumentException : ArgumentParserException
    {
        public ArgumentParserInvalidArgumentException(string msg) : base(msg) { }
    }

    public class ArgumentParserBuilder
    {
        private Dictionary<string, ArgumentConfig> cfgs;
        public ArgumentParserBuilder()
        {
            cfgs = new Dictionary<string, ArgumentConfig>();
        }
        public ArgumentParserBuilder AddConfig(ArgumentConfig cfg)
        {
            cfgs.Add(cfg.RecordName, cfg);
            return this;
        }

        private List<string> GetRequired()
        {
            List<string> list = new List<string>();
            foreach (var item in cfgs)
            {
                if (item.Value.IsRequired)
                {
                    list.Add(item.Value.RecordName);
                }
            }
            return list;
        }

        public CmdLineArgument Build(string[] args)
        {
            //检查必要参数
            var required = GetRequired();
            if ((args == null || args.Length == 0) && required.Count != 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in required)
                {
                    sb.Append(item);
                    sb.Append(", ");
                }
                throw new ArgumentParserInvalidArgumentException("[error] missing cmd: " + sb.ToString());
            }

            CmdLineArgument cmd = new CmdLineArgument();
            if (args == null || args.Length == 0)
            {
                return cmd;
            }

            //对参数循环
            var records = new Dictionary<string, ArgumentCommand>();
            ArgumentCommand curRecord = null;
            for (int i = 0; i < args.Length; i++)
            {
                string item = args[i];
                if (!records.ContainsKey(item) && cfgs.ContainsKey(item))
                {
                    curRecord = new ArgumentCommand(item, new List<string>());
                    records.Add(item, curRecord);
                    continue;
                }
                else if (records.ContainsKey(item) && cfgs.ContainsKey(item))
                {
                    throw new ArgumentParserInvalidArgumentException("[error] repeat command: " + item);
                }
                if (curRecord == null)
                {
                    throw new ArgumentParserNotFindCommandException("[error] not find command: " + item);
                }
                curRecord.Args.Add(item);
            }

            //检查数据有效性
            foreach (KeyValuePair<string, ArgumentConfig> item in cfgs)
            {
                string recordName = item.Key;
                ArgumentConfig cfg = item.Value;

                //必须存在但是没存在
                if (cfg.IsRequired && !records.ContainsKey(recordName))
                {
                    throw new ArgumentParserNotFindCommandException("[error] not find command: " + cfg.RecordName);
                }
                //已存在
                if (records.ContainsKey(recordName))
                {
                    //无参数的指令却给了参数
                    if (cfg.ArgCount == 0 && records[recordName].ArgCount != 0)
                    {
                        throw new ArgumentParserInvalidArgumentException("[error] invalid argument count: " + cfg.RecordName);
                    }
                    //有限的参数但长度不一
                    if (cfg.ArgCount > 0 && records[recordName].ArgCount != cfg.ArgCount)
                    {
                        throw new ArgumentParserInvalidArgumentException("[error] missing arguments: " + cfg.RecordName);
                    }

                    var result = cfg?.CheckInvalidHandler(records[recordName].Args);
                    if (result != null && !result.IsSuccess)
                    {
                        throw new ArgumentParserInvalidArgumentException("[error] invalid arguments: " + result.Infomation);
                    }
                }

            }

            cmd.Records = records;

            return cmd;
        }
    }

    public class CmdLineArgument
    {
        public Dictionary<string, ArgumentCommand> Records;

        public bool HasRecord(string record)
        {
            return Records.ContainsKey(record);
        }
        public ArgumentCommand GetRecord(string record)
        {
            ArgumentCommand rec = null;
            Records.TryGetValue(record, out rec);
            return rec;
        }
        public List<string> GetRecordArgs(string record)
        {
            ArgumentCommand rec = GetRecord(record);
            if (rec == null)
            {
                return null;
            }
            return rec.Args;
        }

    }
}
