using System.Collections.Concurrent;

namespace WebMediaTools.SharedServices
{
    public interface ISharedDictionaryService
    {
        ConcurrentDictionary<string, float> downloadProgressDic { get; }
        ConcurrentDictionary<string, CancellationTokenSource> downloadCancelTokenDic { get; }
        ConcurrentDictionary<string, bool> downloadingDic { get; }
    }

    public class SharedDictionaryService : ISharedDictionaryService
    {
        public ConcurrentDictionary<string, float> downloadProgressDic { get; }
        public ConcurrentDictionary<string, CancellationTokenSource> downloadCancelTokenDic { get; }
        public ConcurrentDictionary<string, bool> downloadingDic { get; }
        public SharedDictionaryService()
        {
            this.downloadProgressDic = new ConcurrentDictionary<string, float>();
            this.downloadCancelTokenDic = new ConcurrentDictionary<string, CancellationTokenSource>();
            this.downloadingDic = new ConcurrentDictionary<string, bool>();
        }
    }
}
