using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.DocumentEditor;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.CodeAnalysis;
using Npgsql;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Newtonsoft.Json.Linq;

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
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                try
                {
                    NpgsqlCommand command2 = new NpgsqlCommand(getOperation, connection);
                    NpgsqlCommand updateCommand = new NpgsqlCommand(getOperation, connection);
                    connection.Open();
                    NpgsqlDataReader reader = updateCommand.ExecuteReader();
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
                            NpgsqlCommand command = new NpgsqlCommand(updatedOperation, connection);
                            NpgsqlDataReader reader2 = command.ExecuteReader();
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

                string queryString = "CREATE TABLE \"" + roomName
                   + "\" (version SERIAL PRIMARY KEY, operation TEXT, clientVersion INTEGER)";             
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    NpgsqlCommand command = new NpgsqlCommand(queryString, connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                    // Create table to track the last saved version.
                    CreateRecordForVersionInfo(connection, roomName);
                }
            }
            else
            {

                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {

                    connection.Open();
                    string queryString = "SELECT * FROM \"" + tableName + "\" WHERE version > " + lastSyncedVersion;
                    NpgsqlCommand command = new NpgsqlCommand(queryString, connection);
                    connection.Open();                 
                    NpgsqlDataReader reader = command.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);
                    List<ActionInfo> actions = GetOperationsQueue(table);
                    return actions;

                }
            }
            return null;
        }
        private void CreateRecordForVersionInfo(NpgsqlConnection connection, String roomName)
        {
            string tableName = "de_version_info";         

            // Check if table exists
            if (!TableExists(tableName))
            {
                // If table doesn't exist, create it
                string createTableQuery = $"CREATE TABLE \"" + tableName + "\" (roomName TEXT, lastSavedVersion INTEGER)"; ;
                using (NpgsqlCommand createTableCommand = new NpgsqlCommand(createTableQuery, connection))
                {
                    createTableCommand.ExecuteNonQuery();
                }
            }

            // Insert record into the table
            string insertQuery = $"INSERT INTO \"" + tableName + "\" (roomName, lastSavedVersion) VALUES (?, ?)";
            using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@roomName", roomName);
                // Set initial version to 0
                insertCommand.Parameters.AddWithValue("@lastSavedVersion", 0);
                insertCommand.ExecuteNonQuery();
            }
            //}

        }
        private bool TableExists(string roomName)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                 NpgsqlCommand command = new NpgsqlCommand($"SELECT CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '"
                    + roomName + "') THEN 1 ELSE 0 END;", connection);
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
            string query = $"INSERT INTO \"{tableName}\" (operation, clientVersion) VALUES (@Operation, @ClientVersion) RETURNING version AS last_id;";

            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {

                NpgsqlCommand command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@Operation", value);
                command.Parameters.AddWithValue("@ClientVersion", action.Version);

                connection.Open();
                int updateVersion = (int)command.ExecuteScalar();
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

        private void UpdateCurrentActionToDB(string tableName, ActionInfo action, NpgsqlConnection connection)
        {
            action.IsTransformed = true;
            string updateQuery = "UPDATE \"" + tableName + "\" SET operation = ? WHERE version = " + action.Version.ToString();
            NpgsqlCommand updateCommand = new NpgsqlCommand(updateQuery, connection);
            updateCommand.Parameters.AddWithValue("@Operation", Newtonsoft.Json.JsonConvert.SerializeObject(action));
            updateCommand.ExecuteNonQuery();
        }

        private static DataTable GetOperationsToTransform(string tableName, int clientVersion, int currentVersion, NpgsqlConnection connection)
        {
            string getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version BETWEEN " + clientVersion + " AND " + currentVersion.ToString();
            NpgsqlCommand command = new NpgsqlCommand(getOperation, connection);
            NpgsqlDataReader reader = command.ExecuteReader();
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
                NpgsqlConnection connection = new NpgsqlConnection(connectionString);
                connection.Open();
                string tableName = fileName;
                int lastSyncedVersion = GetLastedSyncedVersion(connection, fileName);
                string getOperation = "";
                if (partialSave)
                {
                    getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version BETWEEN " + (lastSyncedVersion + 1).ToString() + " AND " + endVersion.ToString();                  
                }
                else
                {
                    getOperation = "SELECT * FROM \"" + tableName + "\" WHERE version > " + lastSyncedVersion;
                }
                NpgsqlCommand command = new NpgsqlCommand(getOperation, connection);
                NpgsqlDataReader reader = command.ExecuteReader();
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

                }
                else
                {
                    UpdateModifiedVersion(tableName, connection, endVersion);

                }

            }
            catch (Exception ex)
            {

            }

        }
        static void UpdateModifiedVersion(string roomName, NpgsqlConnection connection, int lastSavedVersion)
        {
            string tableName = "de_version_info";
            string query = "UPDATE \"" + tableName + "\" SET lastSavedVersion = ? where roomName= '" + roomName + "'";

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@lastSavedVersion", lastSavedVersion);
                command.Parameters.AddWithValue("@roomName", roomName);
                command.ExecuteNonQuery();
            }
        }
        static void DeleteLastModifiedVersion(string roomName, NpgsqlConnection connection)
        {
            string tableName = "de_version_info";
            string query = "DELETE FROM \"" + tableName + "\" WHERE roomName= '" + roomName + "'";

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@roomName", roomName);
                command.ExecuteNonQuery();
            }
        }
        private static int GetLastedSyncedVersion(NpgsqlConnection connection, string roomName)
        {
            string tableName = "de_version_info";
            string query = "SELECT lastSavedVersion FROM \"" + tableName + "\" WHERE roomName ='" + roomName + "'";
            var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@Operation", roomName);            
            return int.Parse(command.ExecuteScalar().ToString());
        }
        private static void DropTable(string documentId, NpgsqlConnection connection)
        {
            try
            {
                //Delete operations record.
                string sqlQuery = "drop table \"" + documentId + "\"";
                var sqlCommand = new NpgsqlCommand(sqlQuery, connection);
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
