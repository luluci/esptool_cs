using esptool_cs.Utility;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace esptool_cs
{
    internal static class Log
    {
        static public LogImpl ProtocolLog = new LogImpl();
        static public LogImpl RawLog = new LogImpl();
    }

    internal class LogImpl : BindableBase
    {
        public ReactiveCollection<string> Data { get; set; }
        public StreamWriter Writer { get; set; }

        public LogImpl()
        {
            Data = new ReactiveCollection<string>();
            Data.AddTo(Disposables);

            Writer = null;
        }

        public void Close()
        {
            //
            if (!(Writer is null))
            {
                Writer.Flush();
                Writer.Close();
                Writer = null;
            }
        }

        public void Open(string path)
        {
            //
            Writer = new StreamWriter(path);
        }


        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)。
                    this.Disposables.Dispose();
                    //
                    Close();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

    }
}
