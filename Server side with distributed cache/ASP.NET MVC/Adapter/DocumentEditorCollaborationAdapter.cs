using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Syncfusion.Collaboration.Core.Extensions;
using Syncfusion.Collaboration.Core.Interfaces;
using Syncfusion.Collaboration.Core.Models;
using Syncfusion.Collaboration.Core.Services;
using Syncfusion.EJ2.DocumentEditor;
using WebApplication1.Controllers;

namespace WebApplication1.Adapter
{
    public class DocumentEditorCollaborationAdapter : ICollaborationAdapter
    {
        public DocumentEditorCollaborationAdapter()
        {
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

        public async Task ProcessSaveRequestAsync(SaveRequest request, CancellationToken cancellationToken)
        {
            //throw new NotImplementedException();
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

               // SaveDocument(stream, "Getting Started.docx");

                stream.Close();
            }

            document.Dispose();

            var actionService = CollaborationServiceContainer.Resolve<IActionService>();
            await actionService.ClearRecordsAsync(request.RoomName, request.PartialSave);
        }

        public async Task SaveOperationsAsync(List<CollaborationAction> actions, string roomName, bool partialSave)
        {
            var message = new SaveRequest
            {
                Actions = actions,
                PartialSave = partialSave,
                RoomName = roomName
            };

            var queue = CollaborationServiceContainer.Resolve<IBackgroundTaskQueue>();
            await queue.QueueBackgroundWorkItemAsync(message);
        }

        public void TransformOperations(List<CollaborationAction> actions)
        {
            var documentActions = actions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)MapGenericToControlAction(x)).ToList();

            documentActions.Where(x => !x.IsTransformed).ToList().ForEach(x => CollaborativeEditingHandler.TransformOperation(x, documentActions));

        }
    }
}