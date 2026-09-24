using Newtonsoft.Json;
using Syncfusion.Collaboration.Core.Extensions;
using Syncfusion.Collaboration.Core.Interfaces;
using Syncfusion.Collaboration.Core.Models;
using Syncfusion.Collaboration.Core.Services;
using Syncfusion.Collaboration.Core.Transports;
using Syncfusion.EJ2.DocumentEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.Cors;
using EJ2WordDocument = Syncfusion.EJ2.DocumentEditor.WordDocument;
namespace WebApplication1.Controllers
{
     [RoutePrefix("api/CollaborativeEditing")]
    public class CollaborativeEditingController : ApiController
    {
        private static string fileLocation;

        private readonly IActionService actionService;
        private readonly ICollaborationAdapter adapter;
        private readonly IActiveTransport transport;
        public CollaborativeEditingController()
        {
            fileLocation = HostingEnvironment.MapPath("~/App_Data");

            actionService = CollaborationServiceContainer.Resolve<IActionService>();

            adapter = CollaborationServiceContainer.Resolve<ICollaborationAdapter>();

            transport = CollaborationServiceContainer.Resolve<IActiveTransport>();
        }
        public CollaborativeEditingController(
            IActionService actionService,
            ICollaborationAdapter adapter,
            IActiveTransport transport)
        {
            fileLocation = HostingEnvironment.MapPath("~/App_Data");
            this.actionService = actionService;
            this.adapter = adapter;
            this.transport = transport;
        }

        [HttpPost]
        [Route("ImportFile")]
        public async Task<HttpResponseMessage> ImportFile(FileInfo param)
        {
            try
            {
                DocumentContent content = new DocumentContent();

                EJ2WordDocument document = GetSourceDocument();

                List<CollaborationAction> collaborationActions = await actionService.GetPendingOperationsAsync( param.roomName,  0, -1);

                List<ActionInfo> actions = collaborationActions.Select(x => (ActionInfo)adapter.MapGenericToControlAction(x)).ToList();

                if (actions != null && actions.Count > 0)
                {
                    document.UpdateActions(actions);
                }

                string sfdt = JsonConvert.SerializeObject(document);

                content.version = 0;
                content.sfdt = sfdt;

                document.Dispose();

                return Request.CreateResponse(HttpStatusCode.OK, content);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse( System.Net.HttpStatusCode.InternalServerError, ex);
            }
        }



        [HttpPost]
        [Route("UpdateAction")]
        public async Task<Syncfusion.EJ2.DocumentEditor.ActionInfo> UpdateAction(Syncfusion.EJ2.DocumentEditor.ActionInfo param)
        {
            // Convert DocumentEditor ActionInfo to CollaborationAction
            CollaborationAction collaborationAction = (CollaborationAction)adapter.MapControlToGenericAction(param);
            // Process through common package
            CollaborationAction modifiedAction = await actionService.AddOperationAsync(collaborationAction, adapter);
            // Convert back to DocumentEditor ActionInfo
            var documentAction = (Syncfusion.EJ2.DocumentEditor.ActionInfo)adapter.MapGenericToControlAction(modifiedAction);           
            await transport.SendToGroupAsync(param.RoomName, "action", documentAction);
            return documentAction;

        }

        [HttpPost]
        [Route("GetActionsFromServer")]       
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

        public class DocumentContent
        {
            public int version { get; set; }

            public string sfdt { get; set; }
        }

        public class FileInfo
        {
            public string fileName { get; set; }

            public string roomName { get; set; }
        }
        internal static EJ2WordDocument GetSourceDocument()
        {
            string path = HostingEnvironment.MapPath("~/App_Data/Giant Panda.docx");

            string extension = Path.GetExtension(path);

            Stream stream = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            EJ2WordDocument document =
                EJ2WordDocument.Load(
                    stream,
                    GetFormatType(extension));

            stream.Dispose();

            return document;
        }


        internal static Syncfusion.EJ2.DocumentEditor.FormatType GetFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                case ".docx":
                case ".docm":
                case ".dotm":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.Docx;
                case ".dot":
                case ".doc":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.Doc;
                case ".rtf":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.Rtf;
                case ".txt":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.Txt;
                case ".xml":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.WordML;
                case ".html":
                    return Syncfusion.EJ2.DocumentEditor.FormatType.Html;
                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }
    }
}