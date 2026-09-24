using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.Collaboration.Core.Interfaces;
using Syncfusion.Collaboration.Core.Models;
using Syncfusion.Collaboration.Core.Services;
using Syncfusion.Collaboration.Core.Transports;
using Syncfusion.EJ2.DocumentEditor;
using System.Data;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CollaborativeEditingController : ControllerBase
    {
        private static string fileLocation;
        private readonly IWebHostEnvironment _hostingEnvironment;
        public readonly IActionService actionService;
        private readonly ICollaborationAdapter adapter;
        private readonly IActiveTransport _transport;

        // Constructor for the CollaborativeEditingController
        public CollaborativeEditingController(IWebHostEnvironment hostingEnvironment,
            IConfiguration config, IActionService actionService, ICollaborationAdapter adapter, IActiveTransport transport)
        {
            _hostingEnvironment = hostingEnvironment;
            fileLocation = _hostingEnvironment.WebRootPath;
            this.adapter = adapter;
            this.actionService = actionService;
            _transport = transport;
        }

        //Import document from wwwroot folder in web server.
        [HttpPost]
        [Route("ImportFile")]
        [EnableCors("AllowAllOrigins")]
        public async Task<string> ImportFile([FromBody] FileInfo param)
        {
            try
            {
                // Create a new instance of DocumentContent to hold the document data
                DocumentContent content = new DocumentContent();

                Syncfusion.EJ2.DocumentEditor.WordDocument document = GetSourceDocument();
                // Get the list of pending operations for the document
                List<CollaborationAction> collaborationActions = await actionService.GetPendingOperationsAsync(param.roomName, 0, -1);

                List<Syncfusion.EJ2.DocumentEditor.ActionInfo> actions =
                    collaborationActions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)adapter.MapGenericToControlAction(x)).ToList();

                if (actions != null && actions.Count > 0)
                {
                    // If there are any pending actions, update the document with these actions
                    document.UpdateActions(actions);
                }
                // Serialize the updated document to SFDT format
                string sfdt = Newtonsoft.Json.JsonConvert.SerializeObject(document);
                content.version = 0;
                content.sfdt = sfdt;
                // Dispose of the document to free resources
                document.Dispose();

                // Return the serialized content as a JSON string
                return Newtonsoft.Json.JsonConvert.SerializeObject(content);
            }
            catch
            {
                return null;
            }
        }

        [HttpPost]
        [Route("UpdateAction")]
        [EnableCors("AllowAllOrigins")]
        public async Task<Syncfusion.EJ2.DocumentEditor.ActionInfo> UpdateAction(Syncfusion.EJ2.DocumentEditor.ActionInfo param)
        {
            // Convert DocumentEditor ActionInfo to CollaborationAction
            CollaborationAction collaborationAction = (CollaborationAction)adapter.MapControlToGenericAction(param);
            // Process through common package
            CollaborationAction modifiedAction = await actionService.AddOperationAsync(collaborationAction, adapter);
            // Convert back to DocumentEditor ActionInfo
            var documentAction = (Syncfusion.EJ2.DocumentEditor.ActionInfo)adapter.MapGenericToControlAction(modifiedAction);
            //    await _hubContext.Clients.Group(param.RoomName).SendAsync("dataReceived", "action", documentAction);

            await _transport.SendToGroupAsync(param.RoomName, "action", documentAction);
            // if(options)

            //  await _transport.SendToConnectionAsync(documentAction.ConnectionId,"save",documentAction);
            return documentAction;

        }

        [HttpPost]
        [Route("GetActionsFromServer")]
        [EnableCors("AllowAllOrigins")]
        public async Task<string> GetActionsFromServer(Syncfusion.EJ2.DocumentEditor.ActionInfo param)
        {
            try
            {
                // Initialize necessary variables from the parameters and helper class
                //int saveThreshold = CollaborativeEditingHelper.SaveThreshold;
                string roomName = param.RoomName;
                int lastSyncedVersion = param.Version;
                int clientVersion = param.Version;

                // Retrieve the database connection
                // IDatabase database = _redisConnection.GetDatabase();

                // Fetch actions that are effective and pending based on the last synced version
                List<CollaborationAction> collaborationActions = await actionService.GetEffectivePendingVersionAsync(roomName, lastSyncedVersion);


                List<Syncfusion.EJ2.DocumentEditor.ActionInfo> actions = collaborationActions.Select(x => (Syncfusion.EJ2.DocumentEditor.ActionInfo)adapter.MapGenericToControlAction(x)).ToList();

                // Increment the version for each action sequentially
                actions.ForEach(action => action.Version = ++clientVersion);

                // Filter actions to only include those that are newer than the client's last known version
                actions = actions.Where(action => action.Version > lastSyncedVersion).ToList();

                // Transform actions that have not been transformed yet
                actions.Where(action => !action.IsTransformed).ToList()
                    .ForEach(action => CollaborativeEditingHandler.TransformOperation(action, actions));

                // Serialize the filtered and transformed actions to JSON and return
                return Newtonsoft.Json.JsonConvert.SerializeObject(actions);
            }
            catch
            {
                // In case of an exception, return an empty JSON object
                return "{}";
            }
        }

        internal static Syncfusion.EJ2.DocumentEditor.WordDocument GetSourceDocument()
        {
            string path = fileLocation + "\\Giant Panda.docx";
            int index = path.LastIndexOf('.');
            string type = index > -1 && index < path.Length - 1 ?
              path.Substring(index) : ".docx";
            Stream stream = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Syncfusion.EJ2.DocumentEditor.WordDocument document = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream, FormatType.Docx);
            stream.Dispose();
            return document;
        }
        public class DocumentContent
        {
            public int version { get; set; }

            public string sfdt { get; set; }

        }
        public class FileInfo
        {
            public string fileName
            {
                get;
                set;
            }
            public string roomName
            {
                get;
                set;
            }
        }


    }
}
