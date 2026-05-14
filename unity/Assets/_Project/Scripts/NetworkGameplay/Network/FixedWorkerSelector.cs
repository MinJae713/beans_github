namespace Colosseum.Network
{
    public class FixedWorkerSelector : IWorkerSelector
    {
        private readonly WorkerInfo _worker;

        public FixedWorkerSelector(
            string workerId = "worker-1",
            string displayName = "HTTP Worker Agent",
            string baseUrl = "http://127.0.0.1:8080")
        {
            _worker = new WorkerInfo
            {
                WorkerId = workerId,
                DisplayName = displayName,
                BaseUrl = baseUrl,
                Enabled = true
            };
        }

        public WorkerInfo SelectWorker()
        {
            return _worker.Enabled ? _worker : null;
        }
    }
}
