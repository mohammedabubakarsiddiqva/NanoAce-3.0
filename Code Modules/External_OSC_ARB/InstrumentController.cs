using System;
using System.Threading;
using Ivi.Visa;
using NationalInstruments.Visa;

namespace BU67833_NEW.External_OSC_ARB
{

    public class InstrumentController : IDisposable
    {
        private MessageBasedSession _session;
        private bool _isOffline;

        public void Connect(string visaAddress, bool offlineMode = false)
        {
            this._isOffline = offlineMode;
            if (this._isOffline)
            {
                Console.WriteLine("[OFFLINE] Pretending to connect to: " + visaAddress);
            }
            else
            {
                using (ResourceManager resourceManager = new ResourceManager())
                    this._session = (MessageBasedSession)resourceManager.Open(visaAddress);
            }
        }

        public int Write(string command)
        {
            if (this._isOffline)
            {
                Console.WriteLine("[OFFLINE] Instrument received command: " + command);
                return 0;
            }
            try
            {
                this._session.TimeoutMilliseconds = 100;
                this._session.FormattedIO.Write(command);
                Thread.Sleep(20);
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public int Read(out string response)
        {
            response = "";
            if (this._isOffline)
            {
                response = "SIMULATED_DATA_3.14159";
                Console.WriteLine("[OFFLINE] Instrument sent response: " + response);
                return 0;
            }
            try
            {
                this._session.TimeoutMilliseconds = 10000;
                response = this._session.FormattedIO.ReadLine();
                Thread.Sleep(10);
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public void Dispose()
        {
            if (this._isOffline)
            {
                Console.WriteLine("[OFFLINE] Disconnected from instrument.");
            }
            else
            {
                if (this._session == null)
                    return;
                this._session.Dispose();
                this._session = (MessageBasedSession)null;
            }
        }
    }

}
