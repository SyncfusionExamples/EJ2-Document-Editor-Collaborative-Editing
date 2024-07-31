using StackExchange.Redis;
using Syncfusion.EJ2.DocumentEditor;
using WebApplication1.Controllers;
using WebApplication1.Model;

namespace WebApplication1.Service
{
    public class QueuedHostedService : BackgroundService
    {
        static string fileLocation;
        static IConnectionMultiplexer _redisConnection;
        public IBackgroundTaskQueue TaskQueue { get; }

        public QueuedHostedService(IBackgroundTaskQueue taskQueue, IWebHostEnvironment hostingEnvironment, IConfiguration config, IConnectionMultiplexer redisConnection)
        {
            TaskQueue = taskQueue;
            fileLocation = hostingEnvironment.WebRootPath;
            _redisConnection = redisConnection;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                SaveInfo workItem = await TaskQueue.DequeueAsync(stoppingToken);

                try
                {
                    ApplyOperationsToSourceDocument(workItem.Action);
                    ClearRecordsFromRedisCache(workItem);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to save the operations to source document", ex);
                }
            }
        }
        private async void ClearRecordsFromRedisCache(SaveInfo workItem)
        {
            //Delete the data in updatekey after updating the values in the document
            IDatabase database = _redisConnection.GetDatabase();
            if (!workItem.PartialSave)
            {
                await database.KeyDeleteAsync(workItem.RoomName);
                await database.KeyDeleteAsync(workItem.RoomName + CollaborativeEditingHelper.RevisionInfoSuffix);
                await database.KeyDeleteAsync(workItem.RoomName + CollaborativeEditingHelper.VersionInfoSuffix);
            }
            //Clear operations from redis cache.
            await database.KeyDeleteAsync(workItem.RoomName + CollaborativeEditingHelper.ActionsToRemoveSuffix);
        }

        public void ApplyOperationsToSourceDocument(List<ActionInfo> actions)
        {
            // Load the document
            Syncfusion.EJ2.DocumentEditor.WordDocument document = CollaborativeEditingController.GetSourceDocument();
            CollaborativeEditingHandler handler = new CollaborativeEditingHandler(document);

            // Process previous items
            if (actions != null && actions.Count > 0)
            {
                foreach (ActionInfo info in actions)
                {
                    if (!info.IsTransformed)
                    {
                        CollaborativeEditingHandler.TransformOperation(info, actions);
                    }
                }

                for (int i = 0; i < actions.Count; i++)
                {
                    //Apply the operation to source document.
                    handler.UpdateAction(actions[i]);
                }
                MemoryStream stream = new MemoryStream();
                Syncfusion.DocIO.DLS.WordDocument doc = WordDocument.Save(Newtonsoft.Json.JsonConvert.SerializeObject(handler.Document));
                doc.Save(stream, Syncfusion.DocIO.FormatType.Docx);

                //Save the document to file location. We can modified the below code and save the document to any location.
                //Save the stream to the location you want.
                SaveDocument(stream, "Getting Started.docx");
                stream.Close();
                document.Dispose();
                handler = null;
            }
        }

        //Document is store in file stream, We can modify the code to store the document to any location based on your requirment.
        private void SaveDocument(Stream document, string fileName)
        {
            string filePath = Path.Combine(fileLocation, fileName);

            using (FileStream file = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                document.Position = 0; // Ensure the stream is at the start
                document.CopyTo(file);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            await base.StopAsync(stoppingToken);
        }
    }
}
