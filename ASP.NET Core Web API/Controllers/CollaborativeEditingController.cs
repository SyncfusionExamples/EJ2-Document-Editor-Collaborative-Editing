using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.DocumentEditor;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.CodeAnalysis;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CollaborativeEditingController : ControllerBase
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHubContext<DocumentEditorHub> _hubContext;
        private static string connectionString;
        private static string fileLocation;

        public CollaborativeEditingController(IWebHostEnvironment hostingEnvironment, IHubContext<DocumentEditorHub> hubContext, IConfiguration config)
        {
            _hostingEnvironment = hostingEnvironment;
            _hubContext = hubContext;
            //Database connection string
            connectionString = config.GetConnectionString("DocumentEditorDatabase");
            fileLocation = _hostingEnvironment.WebRootPath;
        }

        //Import document from wwwroot folder in web server.
        [HttpPost]
        [Route("ImportFile")]
        [EnableCors("AllowAllOrigins")]
        public string ImportFile([FromBody] FileInfo param)
        {
            DocumentContent content = new DocumentContent();
            WordDocument document = GetSourceDocument(param.fileName);
            List<ActionInfo> actions = CreatedTable(param.roomName);
            if (actions != null)
            {
                //Updated pending edit from database to source document.
                document.UpdateActions(actions);
            }
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            content.version = 0;
            content.sfdt = json;
            return Newtonsoft.Json.JsonConvert.SerializeObject(content);
        }

        [HttpPost]
        [Route("UpdateAction")]
        [EnableCors("AllowAllOrigins")]
        public async Task<ActionInfo> UpdateAction([FromBody] ActionInfo param)
        {
            try
            {
                ActionInfo modifiedAction = AddOperationsToTable(param);
                await _hubContext.Clients.Group(param.RoomName).SendAsync("dataReceived", "action", modifiedAction);
                return modifiedAction;
            }
            catch
            {
                return null;
            }
        }

        [HttpPost]
        [Route("GetActionsFromServer")]
        [EnableCors("AllowAllOrigins")]
        public string GetActionsFromServer([FromBody] ActionInfo param)
        {
            string tableName = param.RoomName;
            string getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version > " + param.Version;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    SqlCommand command2 = new SqlCommand(getOperation, connection);
                    SqlCommand updateCommand = new SqlCommand(getOperation, connection);
                    connection.Open();
                    SqlDataReader reader = updateCommand.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);
                    DataTable oldTable = table;
                    if (table.Rows.Count > 0)
                    {
                        int startVersion = int.Parse(table.Rows[0]["version"].ToString());
                        int lowestVersion = GetLowestClientVersion(table);
                        if (startVersion > lowestVersion)
                        {
                            string updatedOperation = "SELECT * FROM \"" + tableName + "\" WHERE version >= " + lowestVersion;
                            SqlCommand command = new SqlCommand(updatedOperation, connection);
                            SqlDataReader reader2 = command.ExecuteReader();
                            table = new DataTable();
                            table.Load(reader2);
                        }
                        List<ActionInfo> actions = GetOperationsQueue(table);
                        foreach (ActionInfo info in actions)
                        {
                            if (!info.IsTransformed)
                            {
                                CollaborativeEditingHandler.TransformOperation(info, actions);
                            }
                        }
                        actions = actions.Where(x => x.Version > param.Version).ToList();
                        return Newtonsoft.Json.JsonConvert.SerializeObject(actions);
                    }
                }
                catch
                {
                    return "{}";
                }
            }
            return "{}";
        }

        private static WordDocument GetSourceDocument(string fileName)
        {
            string path = fileLocation + "\\" + fileName;
            int index = fileName.LastIndexOf('.');
            string type = index > -1 && index < fileName.Length - 1 ?
              fileName.Substring(index) : ".docx";
            Stream stream = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            WordDocument document = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream, GetFormatType(type));
            stream.Dispose();
            return document;
        }

        private List<ActionInfo> CreatedTable(string roomName)
        {

            string tableName = roomName;
            if (!TableExists(tableName))
            {
                string queryString = "CREATE TABLE \"" + tableName + "\" (version int IDENTITY(1,1) PRIMARY KEY, operation nvarchar(max), clientVersion int)";
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        SqlCommand command = new SqlCommand(queryString, connection);
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            else
            {
                string queryString = "SELECT * FROM \"" + tableName + "\"";
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        SqlCommand command = new SqlCommand(queryString, connection);
                        connection.Open();
                        SqlDataReader reader = command.ExecuteReader();
                        DataTable table = new DataTable();
                        table.Load(reader);
                        List<ActionInfo> actions = GetOperationsQueue(table);
                        return actions;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            return null;
        }

        private bool TableExists(string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand($"SELECT CASE WHEN OBJECT_ID('{tableName}', 'U') IS NOT NULL THEN 1 ELSE 0 END", connection);
                connection.Open();
                var result = (int)command.ExecuteScalar();
                return result == 1;
            }
        }

        private ActionInfo AddOperationsToTable(ActionInfo action)
        {
            int clientVersion = action.Version;
            string tableName = action.RoomName;
            string value = Newtonsoft.Json.JsonConvert.SerializeObject(action);
            string query = "INSERT INTO \"" + tableName + "\" (operation, clientVersion) " + "VALUES (@Operation, @ClientVersion); ; SELECT SCOPE_IDENTITY() AS last_id";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.Add("@Operation", SqlDbType.NVarChar).Value = value;
                    command.Parameters.Add("@ClientVersion", SqlDbType.NVarChar).Value = action.Version;
                    connection.Open();
                    int updateVersion = int.Parse(command.ExecuteScalar().ToString());
                    if (updateVersion - clientVersion == 1)
                    {
                        action.Version = updateVersion;
                        UpdateCurrentActionToDB(tableName, action, connection);
                    }
                    else
                    {
                        DataTable table = GetOperationsToTransform(tableName, clientVersion + 1, updateVersion, connection);
                        int startVersion = int.Parse(table.Rows[0]["version"].ToString());
                        int lowestVersion = GetLowestClientVersion(table);
                        if (startVersion > lowestVersion)
                        {
                            table = GetOperationsToTransform(tableName, lowestVersion, updateVersion, connection);
                        }
                        List<ActionInfo> actions = GetOperationsQueue(table);
                        foreach (ActionInfo info in actions)
                        {
                            if (!info.IsTransformed)
                            {
                                CollaborativeEditingHandler.TransformOperation(info, actions);
                            }
                        }
                        action = actions[actions.Count - 1];
                        action.Version = updateVersion;
                        UpdateCurrentActionToDB(tableName, actions[actions.Count - 1], connection);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            return action;
        }

        private void UpdateCurrentActionToDB(string tableName, ActionInfo action, SqlConnection connection)
        {
            action.IsTransformed = true;
            string updateQuery = "UPDATE \"" + tableName + "\" SET operation = @Operation WHERE version = " + action.Version.ToString();
            SqlCommand updateCommand = new SqlCommand(updateQuery, connection);
            updateCommand.Parameters.Add("@Operation", SqlDbType.NVarChar).Value = Newtonsoft.Json.JsonConvert.SerializeObject(action);
            updateCommand.ExecuteNonQuery();
        }

        private static DataTable GetOperationsToTransform(string tableName, int clientVersion, int currentVersion, SqlConnection connection)
        {
            string getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version BETWEEN " + clientVersion + " AND " + currentVersion.ToString();
            SqlCommand command = new SqlCommand(getOperation, connection);
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            return table;
        }

        private static List<ActionInfo> GetOperationsQueue(DataTable table)
        {
            List<ActionInfo> actions = new List<ActionInfo>();
            foreach (DataRow row in table.Rows)
            {
                ActionInfo action = Newtonsoft.Json.JsonConvert.DeserializeObject<ActionInfo>(row["operation"].ToString());
                action.Version = int.Parse(row["version"].ToString());
                action.ClientVersion = int.Parse(row["clientVersion"].ToString());
                actions.Add(action);
            }
            return actions;
        }

        private static int GetLowestClientVersion(DataTable table)
        {
            int clientVersion = int.Parse(table.Rows[0]["clientVersion"].ToString());
            foreach (DataRow row in table.Rows)
            {
                //TODO: Need to optimise version calculation for only untransformed operations
                int version = int.Parse(row["clientVersion"].ToString());
                if (version < clientVersion)
                {
                    clientVersion = version;
                }
            }
            return clientVersion;
        }

        internal static FormatType GetFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                case ".docx":
                case ".docm":
                case ".dotm":
                    return FormatType.Docx;
                case ".dot":
                case ".doc":
                    return FormatType.Doc;
                case ".rtf":
                    return FormatType.Rtf;
                case ".txt":
                    return FormatType.Txt;
                case ".xml":
                    return FormatType.WordML;
                case ".html":
                    return FormatType.Html;
                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }

        /// <summary>
        /// Update editing operation to source document.
        /// </summary>
        public static void UpdateOperationsToSourceDocument(string fileName, string userId)
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            string tableName = fileName;
            string getOperation = "";
           
                getOperation = "SELECT * FROM \"" + tableName + "\"";
            
            SqlCommand command = new SqlCommand(getOperation, connection);
            SqlDataReader reader = command.ExecuteReader();
            DataTable table = new DataTable();
            table.Load(reader);
            if (table.Rows.Count > 0)
            {
                List<ActionInfo> actions = GetOperationsQueue(table);
                foreach (ActionInfo info in actions)
                {
                    if (!info.IsTransformed)
                    {
                        CollaborativeEditingHandler.TransformOperation(info, actions);
                    }
                }
                CollaborativeEditingHandler handler = new CollaborativeEditingHandler(GetSourceDocument(fileName));
                for (int i = 0; i < actions.Count; i++)
                {
                    //Apply remote operation to source document
                    handler.UpdateAction(actions[i]);
                }
                MemoryStream stream = new MemoryStream();
                Syncfusion.DocIO.DLS.WordDocument doc = WordDocument.Save(Newtonsoft.Json.JsonConvert.SerializeObject(handler.Document));
                doc.Save(stream, Syncfusion.DocIO.FormatType.Docx);
                stream.Position = 0;
               //Add code to update modified document to source document location.
            }
            //Clear DB record
            DropTable(fileName, connection);
            
        }

        private static void DropTable(string documentId, SqlConnection connection)
        {
            try
            {
                //Delete operations record.
                string sqlQuery = "drop table \"" + documentId + "\"";
                var sqlCommand = new SqlCommand(sqlQuery, connection);
                sqlCommand.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

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

    public class DocumentContent
    {
        public int version { get; set; }

        public string sfdt { get; set; }
    }
}
