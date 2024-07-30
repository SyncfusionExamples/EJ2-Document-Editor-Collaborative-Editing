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
        private static byte saveThreshold = 200;

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
            int lastSyncedVersion = 0;
            List<ActionInfo> actions = CreatedTable(param.roomName, out lastSyncedVersion);
            if (actions != null)
            {
                //Updated pending edit from database to source document.
                document.UpdateActions(actions);
            }
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            content.version = lastSyncedVersion;
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

        private List<ActionInfo> CreatedTable(string roomName, out int lastSyncedVersion)
        {
            lastSyncedVersion = 0;
            string tableName = roomName;
            if (!TableExists(tableName))
            {

                string queryString = "CREATE TABLE \"" + tableName + "\" (version int IDENTITY(1,1) PRIMARY KEY, operation nvarchar(max), clientVersion int)";
                using (SqlConnection connection = new SqlConnection(connectionString))
                {

                    SqlCommand command = new SqlCommand(queryString, connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                    // Create table to track the last saved version.
                    CreateRecordForVersionInfo(connection, roomName);

                }
            }
            else
            {

                using (SqlConnection connection = new SqlConnection(connectionString))
                {

                    connection.Open();
                    lastSyncedVersion = GetLastedSyncedVersion(connection, tableName);
                    string queryString = "SELECT * FROM \"" + tableName + "\" WHERE version > " + lastSyncedVersion;
                    SqlCommand command = new SqlCommand(queryString, connection);
                    SqlDataReader reader = command.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);
                    List<ActionInfo> actions = GetOperationsQueue(table);
                    return actions;

                }
            }
            return null;
        }
        private void CreateRecordForVersionInfo(SqlConnection connection, String roomName)
        {
            string tableName = "de_version_info";

                if (!TableExists(tableName))
                {
                // If table doesn't exist, create it
                string createTableQuery = $"CREATE TABLE \"{tableName}\" (roomName VARCHAR(MAX), lastSavedVersion INTEGER)";
                using (SqlCommand createTableCommand = new SqlCommand(createTableQuery, connection))
                {
                    createTableCommand.ExecuteNonQuery();
                }
            }

            // Insert record into the table
            string insertQuery = $"INSERT INTO \"{tableName}\" (roomName, lastSavedVersion) VALUES (@roomName, @lastSavedVersion)";
            using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@roomName", roomName);
                // Set initial version to 0
                insertCommand.Parameters.AddWithValue("@lastSavedVersion", 0);
                insertCommand.ExecuteNonQuery();
            }
            //}

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
                if (updateVersion % saveThreshold == 0)
                {
                    UpdateOperationsToSourceDocument(tableName, HttpContext.Session.GetString("UserId"), true, updateVersion);
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
        public static void UpdateOperationsToSourceDocument(string fileName, string userId, bool partialSave, int endVersion)
        {
            try
            {
                SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                string tableName = fileName;
                int lastSyncedVersion = GetLastedSyncedVersion(connection, fileName);
                string getOperation = "";
                if (partialSave)
                {
                    getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version BETWEEN " + (lastSyncedVersion + 1).ToString() + " AND " + endVersion.ToString();
                    //getOperation = "SELECT Top (" + saveThreshold.ToString() + ") * FROM \"" + tableName + "\"";
                }
                else
                {
                    getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version > " + lastSyncedVersion;
                }
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
                    //CollaborativeEditingHandler handler = new CollaborativeEditingHandler(GetDocumentFromDatabase(fileName, GetSelectedDocumentOwner(userId, fileName, connection)));
                    var currentDirectory = System.IO.Directory.GetCurrentDirectory();
                    int index = fileName.LastIndexOf('.');
                    string type = index > -1 && index < fileName.Length - 1 ?
                    fileName.Substring(index) : ".docx";
                    Stream stream1 = System.IO.File.Open(currentDirectory + "\\" + fileName, FileMode.Open, FileAccess.ReadWrite);
                    Syncfusion.EJ2.DocumentEditor.WordDocument document = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream1, GetFormatType(type));
                    stream1.Close();
                    CollaborativeEditingHandler handler = new CollaborativeEditingHandler(document);
                    for (int i = 0; i < actions.Count; i++)
                    {
                        //Console.WriteLine(i);
                        handler.UpdateAction(actions[i]);
                    }
                    MemoryStream stream = new MemoryStream();
                    Syncfusion.DocIO.DLS.WordDocument doc = WordDocument.Save(Newtonsoft.Json.JsonConvert.SerializeObject(handler.Document));
                    doc.Save(stream, Syncfusion.DocIO.FormatType.Docx);
                    stream.Position = 0;
                    byte[] data = stream.ToArray();
                    System.IO.File.WriteAllBytes(currentDirectory + "\\output.docx", data);
                    stream.Close();
                    if (!partialSave)
                    {
                        endVersion = actions[actions.Count - 1].Version;
                    }                  
                    doc.Close();                    
                }
                if (!partialSave)
                {
                    DeleteLastModifiedVersion(tableName, connection);
                    DropTable(fileName, connection);
                    
                }else
                {
                    UpdateModifiedVersion(tableName, connection, endVersion);

                }
                
            }
            catch (Exception ex)
            {
              
            }

        }
        static void UpdateModifiedVersion(string roomName, SqlConnection connection, int lastSavedVersion)
        {
            string tableName = "de_version_info";
            string query = "UPDATE [" + tableName + "] SET lastSavedVersion = @lastSavedVersion WHERE roomName = @roomName";
            using (SqlCommand command = new SqlCommand(query, connection)) 
            {

                command.Parameters.AddWithValue("@lastSavedVersion", lastSavedVersion);
                command.Parameters.AddWithValue("@roomName", roomName);
                command.ExecuteNonQuery();                      
            }
        }
        static void DeleteLastModifiedVersion(string roomName, SqlConnection connection)
        {
            string tableName = "de_version_info";
            string query = "DELETE FROM [" + tableName + "] WHERE roomName = @roomName";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@roomName", roomName);
                command.ExecuteNonQuery();
            }
        }
        private static int GetLastedSyncedVersion(SqlConnection connection, string roomName)
        {
            string tableName = "de_version_info";
            string query = "SELECT lastSavedVersion FROM \"" + tableName + "\" WHERE roomName ='" + roomName + "'";
            var command = new SqlCommand(query, connection);
            command.Parameters.Add("@roomName", SqlDbType.NVarChar).Value = roomName;           
            return int.Parse(command.ExecuteScalar().ToString());
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
