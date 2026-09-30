using Newtonsoft.Json;
using Syncfusion.Collaboration.Core.Interfaces;
using Syncfusion.Collaboration.Core.Models;
using Syncfusion.Collaboration.Core.Services;
using Syncfusion.Collaboration.Core.Transports;
using Syncfusion.EJ2.DocumentEditor;
using WebApplication1.Controllers;

namespace WebApplication1.Adapter
{
    public class DocumentEditorCollaborationAdapter : ICollaborationAdapter
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IBackgroundTaskQueue saveTaskQueue;

        static string fileLocation;
        private readonly IWebHostEnvironment _hostingEnvironment;
        public DocumentEditorCollaborationAdapter(IWebHostEnvironment hostingEnvironment, IBackgroundTaskQueue saveTaskQueue, IServiceScopeFactory serviceScopeFactory)
        {
            _hostingEnvironment = hostingEnvironment;
            fileLocation = _hostingEnvironment.WebRootPath;
            this.saveTaskQueue = saveTaskQueue;
            this.serviceScopeFactory = serviceScopeFactory;
        }
        public CollaborationAction MapControlToGenericAction(object controlAction)
        {
            var action = (Syncfusion.EJ2.DocumentEditor.ActionInfo)controlAction;

            return new CollaborationAction
            {
                RoomName = action.RoomName,
                ConnectionId = action.ConnectionId,
                CurrentUser = action.CurrentUser,
                Version = action.Version,
                ClientVersion = action.ClientVersion,
                IsTransformed = action.IsTransformed,
                Data = JsonConvert.SerializeObject(action.Operations)
            };
        }

        public object MapGenericToControlAction(CollaborationAction action)
        {
            return new Syncfusion.EJ2.DocumentEditor.ActionInfo
            {
                RoomName = action.RoomName,
                ConnectionId = action.ConnectionId,
                CurrentUser = action.CurrentUser,
                Version = action.Version,
                ClientVersion = action.ClientVersion,
                IsTransformed = action.IsTransformed,
                Operations = JsonConvert.DeserializeObject<List<DocumentOperation>>(action.Data)
            };
        }


        public void TransformOperations(List<CollaborationAction> actions)
        {
            var documentActions = actions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)MapGenericToControlAction(x)).ToList();

            documentActions.Where(x => !x.IsTransformed).ToList().ForEach(x => CollaborativeEditingHandler.TransformOperation(x, documentActions));
        }

        public async Task SaveOperationsAsync(List<CollaborationAction> actions, string roomName, bool partialSave)
        {
            var documentActions = actions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)MapGenericToControlAction(x)).ToList();

            var message = new SaveRequest
            {
                Actions = actions,
                PartialSave = partialSave,
                RoomName = roomName
            };

            await saveTaskQueue.QueueBackgroundWorkItemAsync(message);


        }
        public async Task ProcessSaveRequestAsync(SaveRequest request, CancellationToken ct)
        {
            Console.WriteLine("save called");
            // You can get the document master document 
            Syncfusion.EJ2.DocumentEditor.WordDocument document = CollaborativeEditingController.GetSourceDocument();
            CollaborativeEditingHandler handler = new CollaborativeEditingHandler(document);
            //Get actions from Redis
            var actions = request.Actions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)MapGenericToControlAction(x)).ToList();


            if (actions.Count > 0)
            {
                foreach (var action in actions)
                {
                    if (!action.IsTransformed)
                    {
                        CollaborativeEditingHandler.TransformOperation(action, actions);
                    }
                }
                //Apply the actions to document 
                foreach (var action in actions)
                {
                    handler.UpdateAction(action);
                }

                MemoryStream stream = new MemoryStream();
                //save the updated document in the loaction as per your need. 

                Syncfusion.DocIO.DLS.WordDocument doc = WordDocument.Save(Newtonsoft.Json.JsonConvert.SerializeObject(handler.Document));

                doc.Save(stream, Syncfusion.DocIO.FormatType.Docx);

                SaveDocument(stream, "Getting Started.docx");

                stream.Close();
            }

            document.Dispose();


            var scope = serviceScopeFactory.CreateScope();

            var actionService = scope.ServiceProvider.GetRequiredService<IActionService>();
            var _transport = scope.ServiceProvider.GetRequiredService<IActiveTransport>();
            await actionService.ClearRecordsAsync(request.RoomName, request.PartialSave);

            //   await _transport.SendToConnectionAsync(actions[0].ConnectionId, "save", actions[0]);


        }

        //Document is store in file stream, We can modify the code to store the document to any location based on your requirment.
        private void SaveDocument(Stream document, string fileName)
        {
            string filePath;
            if (Path.IsPathRooted(fileName))
            {
                filePath = fileName;
            }
            else
            {
                filePath = Path.Combine(fileLocation, fileName);
            }

            // Ensure target directory exists
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (FileStream file = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                document.Position = 0; // Ensure the stream is at the start
                document.CopyTo(file);
            }
        }

    }
}
