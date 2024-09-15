using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs.EspBootloader
{
    internal enum Command : byte
    {
        None = 0,
        FLASH_BEGIN = 0x02,

        READ_REG = 0x0A,
    }
    internal static class CommandHelper
    {
        static public Command[] CommandConvertTable { get; set; }

        static CommandHelper()
        {
            MakeCommandConvertTable();
        }

        public static void MakeCommandConvertTable()
        {
            CommandConvertTable = new Command[byte.MaxValue+1];

            //for (int idx = 0; idx < (int)Command.MAX; idx++)
            //{
            //}
            foreach (var cmdobj in Enum.GetValues(typeof(Command)))
            {
                var cmd = (Command)cmdobj;
                CommandConvertTable[(int)cmd] = cmd;
            }
        }
    }
}
