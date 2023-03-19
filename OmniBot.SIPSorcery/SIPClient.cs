using Microsoft.Extensions.Logging;

namespace OmniBot.SIPSorcery
{
    public class SIPClient
    {
        private readonly ILoggerFactory _loggerFactory;

        public SIPClient(ILoggerFactory loggerFactory)
        {
            SIPSorceryInitializer.CheckInitiatlization();
            
            _loggerFactory = loggerFactory;
        }

        public async Task<SIPCall> Call(string address, string username = null, string password = null)
        {
            var call =  new SIPCall(_loggerFactory, address, username, password);

            await call.Connect();

            return call;
        }
    }
}
