using Microsoft.AspNetCore.Mvc;
using System.Text;
using Syncfusion.EJ2.DocumentEditor;
using WDocument = Syncfusion.DocIO.DLS.WordDocument;
using WFormatType = Syncfusion.DocIO.FormatType;
using Syncfusion.EJ2.SpellChecker;
using Microsoft.AspNetCore.Cors;
using WebApplication1.Hubs;
using Microsoft.AspNetCore.Http.Features;
using System.Net.Http.Headers;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentEditorController : ControllerBase
    {
        string connectionString;
        public DocumentEditorController(IWebHostEnvironment hostingEnvironment, IConfiguration config)
        {
            this.connectionString = config.GetConnectionString("DocumentEditorDatabase");
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("Import")]
        public string Import(IFormCollection data)
        {
            if (data.Files.Count == 0)
                return null;
            Stream stream1 = new MemoryStream();
            IFormFile file = data.Files[0];
            int index = file.FileName.LastIndexOf('.');
            string type = index > -1 && index < file.FileName.Length - 1 ?
                file.FileName.Substring(index) : ".docx";
            file.CopyTo(stream1);
            stream1.Position = 0;

            WordDocument document = WordDocument.Load(stream1, GetFormatType(type.ToLower()));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }

        public class CustomParams
        {
            public string fileName
            {
                get;
                set;
            }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("SpellCheck")]
        public string SpellCheck([FromBody] SpellCheckJsonData spellChecker)
        {
            try
            {
                SpellChecker spellCheck = new SpellChecker();
                spellCheck.GetSuggestions(spellChecker.LanguageID, spellChecker.TexttoCheck, spellChecker.CheckSpelling, spellChecker.CheckSuggestion, spellChecker.AddWord);
                return Newtonsoft.Json.JsonConvert.SerializeObject(spellCheck);
            }
            catch
            {
                return "{\"SpellCollection\":[],\"HasSpellingError\":false,\"Suggestions\":null}";
            }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("SpellCheckByPage")]
        public string SpellCheckByPage([FromBody] SpellCheckJsonData spellChecker)
        {
            try
            {
                SpellChecker spellCheck = new SpellChecker();
                spellCheck.CheckSpelling(spellChecker.LanguageID, spellChecker.TexttoCheck);
                return Newtonsoft.Json.JsonConvert.SerializeObject(spellCheck);
            }
            catch
            {
                return "{\"SpellCollection\":[],\"HasSpellingError\":false,\"Suggestions\":null}";
            }
        }

        public class SpellCheckJsonData
        {
            public int LanguageID { get; set; }
            public string TexttoCheck { get; set; }
            public bool CheckSpelling { get; set; }
            public bool CheckSuggestion { get; set; }
            public bool AddWord { get; set; }

        }
        public class UploadDocument
        {
            public string DocumentName { get; set; }
        }


        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("MailMerge")]
        public string MailMerge([FromBody] ExportData exportData)
        {
            Byte[] data = Convert.FromBase64String(exportData.documentData.Split(',')[1]);
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            stream.Position = 0;
            try
            {
                Syncfusion.DocIO.DLS.WordDocument document = new Syncfusion.DocIO.DLS.WordDocument(stream, Syncfusion.DocIO.FormatType.Docx);
                document.MailMerge.RemoveEmptyGroup = true;
                document.MailMerge.RemoveEmptyParagraphs = true;
                document.MailMerge.ClearFields = true;
                document.MailMerge.Execute(CustomerDataModel.GetAllRecords());
                document.Save(stream, Syncfusion.DocIO.FormatType.Docx);
            }
            catch (Exception ex)
            { }
            string sfdtText = "";
            Syncfusion.EJ2.DocumentEditor.WordDocument document1 = Syncfusion.EJ2.DocumentEditor.WordDocument.Load(stream, Syncfusion.EJ2.DocumentEditor.FormatType.Docx);
            sfdtText = Newtonsoft.Json.JsonConvert.SerializeObject(document1);
            document1.Dispose();
            return sfdtText;
        }
        public class CustomerDataModel
        {
            public static List<Customer> GetAllRecords()
            {
                List<Customer> customers = new List<Customer>();
                customers.Add(new Customer("9072379", "50%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "2000", "19072379", "Folk och fä HB", "100000", "440", "32.34", "472.34", "28023", "12000", "2020-11-07 00:00:00", "2020-12-07 00:00:00"));
                customers.Add(new Customer("9072378", "20%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "", "2", "19072369", "Maersk", "140000", "245", "20", "265", "28024", "12400", "2020-11-31 00:00:00", "2020-12-22300:00:00"));
                customers.Add(new Customer("9072377", "30%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "100", "19072879", "Mediterranean Shipping Company", "104000", "434", "50.43", "484.43", "28025", "10000", "2020-11-07 00:00:00", "2020-12-02 00:00:00"));
                customers.Add(new Customer("9072393", "10%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "2050", "19072378", "China Ocean Shipping Company", "175000", "500", "32", "532", "28026", "17000", "2020-09-23 00:00:00", "2020-10-09 00:00:00"));
                customers.Add(new Customer("9072377", "14%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "2568", "19072380", "CGM", "155000", "655", "20.54", "675.54", "28027", "13000", "2020-10-11 00:00:00", "2020-11-17 00:00:00"));
                customers.Add(new Customer("9072376", "0%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "1532", "19072345", " Hapag-Lloyd", "106500", "344", "30", "374", "28028", "14500", "2020-06-17 00:00:00", "2020-07-07 00:00:00"));
                customers.Add(new Customer("9072369", "05%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "4462", "190723452", "Ocean Network Express", "100054", "541", "50", "591", "28029", "16500", "2020-04-07 00:00:00", "2020-05-07 00:00:00"));
                customers.Add(new Customer("9072359", "4%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "27547", "190723713", "Evergreen Line", "124000", "800", "10.23", "810.23", "28030", "12500", "2020-03-07 00:00:00", "2020-04-07 00:00:00"));
                customers.Add(new Customer("9072380", "20%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "7582", "19072312", "Yang Ming Marine Transport", "1046000", "290", "10", "300", "27631", "12670", "2020-11-10 00:00:00", "2020-12-13 00:00:00"));
                customers.Add(new Customer("9072381", "42%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "862", "19072354", "Hyundai Merchant Marine", "145000", "800", "10.23", "810.23", "28032", "45000", "2020-10-17 00:00:00", "2020-12-23 00:00:00"));
                customers.Add(new Customer("9072391", "84%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "82", "19072364", "Pacific International Line", "10094677", "344", "30", "374", "28033", "16500", "2020-11-14 00:00:00", "2020-12-21 00:00:00"));
                customers.Add(new Customer("9072392", "92%", "C/ Araquil, 67", "Madrid", "22020-08-10 00:00:00", "Spain", "Brittania", "82", "19072385", "Österreichischer Lloyd", "104270", "500", "32", "532", "28034", "156500", "2020-06-07 00:00:00", "2020-07-07 00:00:00"));
                return customers;
            }
        }
        public class Customer
        {
            public string CustomerID { get; set; }
            public string ProductName { get; set; }
            public string Quantity { get; set; }
            public string ShipName { get; set; }
            public string UnitPrice { get; set; }
            public string Discount { get; set; }
            public string ShipAddress { get; set; }
            public string ShipCity { get; set; }
            public string OrderDate { get; set; }
            public string ShipCountry { get; set; }
            public string OrderId { get; set; }
            public string Subtotal { get; set; }
            public string Freight { get; set; }
            public string Total { get; set; }
            public string ShipPostalCode { get; set; }
            public string RequiredDate { get; set; }
            public string ShippedDate { get; set; }
            public string ExtendedPrice { get; set; }
            public Customer(string orderId, string discount, string shipAddress, string shipCity, string orderDate, string shipCountry, string productName, string quantity, string customerID, string shipName, string unitPrice, string subtotal, string freight, string total, string shipPostalCode, string extendedPrice, string requiredDate, string shippedDate)
            {
                this.CustomerID = customerID;
                this.ProductName = productName;
                this.Quantity = quantity;
                this.ShipName = shipName;
                this.UnitPrice = unitPrice;
                this.Discount = discount;
                this.ShipAddress = shipAddress;
                this.ShipCity = shipCity;
                this.OrderDate = orderDate;
                this.ShipCountry = shipCountry;
                this.OrderId = orderId;
                this.Subtotal = subtotal;
                this.Freight = freight;
                this.Total = total;
                this.ShipPostalCode = shipPostalCode;
                this.ShippedDate = shippedDate;
                this.RequiredDate = requiredDate;
                this.ExtendedPrice = extendedPrice;
            }
        }
        public class ExportData
        {
            public string fileName { get; set; }
            public string documentData { get; set; }
        }

        public class CustomParameter
        {
            public string content { get; set; }
            public string type { get; set; }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("SystemClipboard")]
        public string SystemClipboard([FromBody] CustomParameter param)
        {
            if (param.content != null && param.content != "")
            {
                try
                {
                    WordDocument document = WordDocument.LoadString(param.content, GetFormatType(param.type.ToLower()));
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
                    document.Dispose();
                    return json;
                }
                catch (Exception)
                {
                    return "";
                }
            }
            return "";
        }

        public class CustomRestrictParameter
        {
            public string passwordBase64 { get; set; }
            public string saltBase64 { get; set; }
            public int spinCount { get; set; }
        }
        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("RestrictEditing")]
        public string[] RestrictEditing([FromBody] CustomRestrictParameter param)
        {
            if (param.passwordBase64 == "" && param.passwordBase64 == null)
                return null;
            return WordDocument.ComputeHash(param.passwordBase64, param.saltBase64, param.spinCount);
        }


        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("LoadDefault")]
        public string LoadDefault()
        {
            Stream stream = System.IO.File.OpenRead("App_Data/GettingStarted.docx");
            stream.Position = 0;

            WordDocument document = WordDocument.Load(stream, FormatType.Docx);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("LoadDocument")]
        public string LoadDocument([FromForm] UploadDocument uploadDocument)
        {
            string path = "";
            string documentPath = Path.Combine(path, uploadDocument.DocumentName);
            Stream stream = null;
            if (System.IO.File.Exists(documentPath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(documentPath);
                stream = new MemoryStream(bytes);
            }
            else
            {
                bool result = Uri.TryCreate(uploadDocument.DocumentName, UriKind.Absolute, out Uri uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result)
                {
                    stream = GetDocumentFromURL(uploadDocument.DocumentName).Result;
                    if (stream != null)
                        stream.Position = 0;
                }
            }
            WordDocument document = WordDocument.Load(stream, FormatType.Docx);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(document);
            document.Dispose();
            return json;
        }
        async Task<MemoryStream> GetDocumentFromURL(string url)
        {
            var client = new HttpClient(); ;
            var response = await client.GetAsync(url);
            var rawStream = await response.Content.ReadAsStreamAsync();
            if (response.IsSuccessStatusCode)
            {
                MemoryStream docStream = new MemoryStream();
                rawStream.CopyTo(docStream);
                return docStream;
            }
            else { return null; }
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
        internal static WFormatType GetWFormatType(string format)
        {
            if (string.IsNullOrEmpty(format))
                throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            switch (format.ToLower())
            {
                case ".dotx":
                    return WFormatType.Dotx;
                case ".docx":
                    return WFormatType.Docx;
                case ".docm":
                    return WFormatType.Docm;
                case ".dotm":
                    return WFormatType.Dotm;
                case ".dot":
                    return WFormatType.Dot;
                case ".doc":
                    return WFormatType.Doc;
                case ".rtf":
                    return WFormatType.Rtf;
                case ".html":
                    return WFormatType.Html;
                case ".txt":
                    return WFormatType.Txt;
                case ".xml":
                    return WFormatType.WordML;
                case ".odt":
                    return WFormatType.Odt;
                default:
                    throw new NotSupportedException("EJ2 DocumentEditor does not support this file format.");
            }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("Save")]
        public void Save([FromBody] SaveParameter data)
        {
            string name = data.FileName;
            string format = RetrieveFileType(name);
            if (string.IsNullOrEmpty(name))
            {
                name = "Document1.doc";
            }
            WDocument document = WordDocument.Save(data.Content);
            FileStream fileStream = new FileStream(name, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            document.Save(fileStream, GetWFormatType(format));
            document.Close();
            fileStream.Close();
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("ExportSFDT")]
        public FileStreamResult ExportSFDT([FromBody] SaveParameter data)
        {
            string name = data.FileName;
            string format = RetrieveFileType(name);
            if (string.IsNullOrEmpty(name))
            {
                name = "Document1.doc";
            }
            WDocument document = WordDocument.Save(data.Content);
            return SaveDocument(document, format, name);
        }

        private string RetrieveFileType(string name)
        {
            int index = name.LastIndexOf('.');
            string format = index > -1 && index < name.Length - 1 ?
                name.Substring(index) : ".doc";
            return format;
        }

        public class SaveParameter
        {
            public string Content { get; set; }
            public string FileName { get; set; }
        }

        [AcceptVerbs("Post")]
        [HttpPost]
        [EnableCors("AllowAllOrigins")]
        [Route("Export")]
        public FileStreamResult Export(IFormCollection data)
        {
            if (data.Files.Count == 0)
                return null;
            string fileName = this.GetValue(data, "filename");
            string name = fileName;
            string format = RetrieveFileType(name);
            if (string.IsNullOrEmpty(name))
            {
                name = "Document1";
            }
            WDocument document = this.GetDocument(data);
            return SaveDocument(document, format, fileName);
        }

        private FileStreamResult SaveDocument(WDocument document, string format, string fileName)
        {
            Stream stream = new MemoryStream();
            string contentType = "";
            if (format == ".pdf")
            {
                contentType = "application/pdf";
            }
            else
            {
                WFormatType type = GetWFormatType(format);
                switch (type)
                {
                    case WFormatType.Rtf:
                        contentType = "application/rtf";
                        break;
                    case WFormatType.WordML:
                        contentType = "application/xml";
                        break;
                    case WFormatType.Html:
                        contentType = "application/html";
                        break;
                    case WFormatType.Dotx:
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template";
                        break;
                    case WFormatType.Docx:
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        break;
                    case WFormatType.Doc:
                        contentType = "application/msword";
                        break;
                    case WFormatType.Dot:
                        contentType = "application/msword";
                        break;
                }
                document.Save(stream, type);
            }
            document.Close();
            stream.Position = 0;
            return new FileStreamResult(stream, contentType)
            {
                FileDownloadName = fileName
            };
        }

        private string GetValue(IFormCollection data, string key)
        {
            if (data.ContainsKey(key))
            {
                string[] values = data[key];
                if (values.Length > 0)
                {
                    return values[0];
                }
            }
            return "";
        }
        private WDocument GetDocument(IFormCollection data)
        {
            Stream stream = new MemoryStream();
            IFormFile file = data.Files[0];
            file.CopyTo(stream);
            stream.Position = 0;

            WDocument document = new WDocument(stream, WFormatType.Docx);
            stream.Dispose();
            return document;
        }




        [AcceptVerbs("Post")]
        [Route("UploadFile")]
        public IActionResult UploadFile(IList<IFormFile> UploadFiles)
        {
            try
            {
                string userId = HttpContext.Session.GetString("UserId");

                foreach (var file in UploadFiles)
                {
                    if (UploadFiles != null)
                    {
                        var filename = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                        try
                        {
                            MemoryStream stream = new MemoryStream();
                            file.CopyTo(stream);
                            stream.Position = 0;
                            CheckAndCreateTableForDocument(userId, filename, stream, "", "", "");
                            stream.Dispose();
                        }
                        catch
                        {
                            Response.Clear();
                            Response.StatusCode = 204;
                            Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File already exists.";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Response.Clear();
                Response.ContentType = "application/json; charset=utf-8";
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "No Content";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;
            }
            return Content("");
        }

        private void CheckAndCreateTableForDocument(string tableName, string documemtName, MemoryStream stream, string sharedBy, string documentId, string documentOwner)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (!TableExists(tableName, connection))
                {
                    string query = "CREATE TABLE \"" + tableName + "\" (documentId nvarchar(254),documentName nvarchar(254), documentData varbinary(max), createdOn nvarchar(254),sharedWith nvarchar(max), sharedBy nvarchar(max), documentOwner nvarchar(max))";
                    var createCommand = new SqlCommand(query, connection);
                    createCommand.ExecuteNonQuery();
                }

                string uploadDocument = "Insert into \"" + tableName + "\" (documentId, documentName, documentData, createdOn, sharedWith, sharedBy, documentOwner) VALUES (@documentId,@documentName, @documentData, @createdOn, @sharedWith, @sharedBy, @documentOwner)";
                var command = new SqlCommand(uploadDocument, connection);
                command.Parameters.Add("@documentId", SqlDbType.NVarChar).Value = string.IsNullOrEmpty(documentId) ? Regex.Replace(Guid.NewGuid().ToString(), "-", "") : documentId;
                command.Parameters.Add("@documentName", SqlDbType.NVarChar).Value = documemtName;
                command.Parameters.Add("@documentData", SqlDbType.VarBinary).Value = stream != null ? stream.ToArray() : new byte[0];
                command.Parameters.Add("@createdOn", SqlDbType.NVarChar).Value = DateTime.Now;
                command.Parameters.Add("@sharedWith", SqlDbType.NVarChar).Value = "";
                command.Parameters.Add("@sharedBy", SqlDbType.NVarChar).Value = sharedBy;
                command.Parameters.Add("@documentOwner", SqlDbType.NVarChar).Value = documentOwner;

                command.ExecuteNonQuery();
            }

        }

        private bool TableExists(string tableName, SqlConnection connection)
        {
            string query = "SELECT CASE WHEN OBJECT_ID('\"" + tableName + "\"', 'U') IS NOT NULL THEN 1 ELSE 0 END";
            var command = new SqlCommand(query, connection);

            var result = (int)command.ExecuteScalar();
            return result == 1;
        }

        [AcceptVerbs("Post")]
        [Route("RemoveFile")]
        public IActionResult RemoveFile(IList<IFormFile> UploadFiles)
        {
            try
            {
                string userId = HttpContext.Session.GetString("UserId");
                foreach (var file in UploadFiles)
                {
                    var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    using (var connection = new SqlConnection(connectionString))
                    {
                        string query = "DELETE FROM \"" + userId + "\" WHERE documentName=" + fileName;
                        connection.Open();
                        var command = new SqlCommand(query, connection);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                Response.Clear();
                Response.StatusCode = 200;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File removed successfully";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;
            }
            return Content("");
        }

        [AcceptVerbs("Post")]
        [Route("DeleteFile")]
        public IActionResult DeleteFile(DocumentShareInfo info)
        {

            string userId = HttpContext.Session.GetString("UserId");

            var fileName = info.DocumentName;
            using (var connection = new SqlConnection(connectionString))
            {
                string query = "Select sharedWith FROM \"" + userId + "\" documentName='" + fileName + "'";
                connection.Open();
                var command = new SqlCommand(query, connection);
                string sharedWith = command.ExecuteScalar().ToString();

                string[] users = sharedWith.Split(";");

                for (int i = 0; i < users.Length; i++)
                {

                    string updateSharedInfo = "Select * from user_info where email=@email";
                    var updateCommand = new SqlCommand(updateSharedInfo, connection);
                    updateCommand.Parameters.Add("@email", SqlDbType.NVarChar).Value = users[i];

                    SqlDataReader reader = updateCommand.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);
                    if (table.Rows.Count > 0)
                    {
                        string remoteUserId = table.Rows[0]["userId"].ToString();
                        string deleteDocument = "Delete From \"" + remoteUserId + "\" WHERE documentName='" + info.DocumentId + "'";
                        var deleteCommand = new SqlCommand(deleteDocument, connection);
                        deleteCommand.ExecuteNonQuery();
                    }
                }

                string sourceDelete = "DELETE FROM \"" + userId + "\" WHERE documentId=" + info.DocumentId;
                var sourceDeleteCommand = new SqlCommand(sourceDelete, connection);
                sourceDeleteCommand.ExecuteNonQuery();

            }
            return Content("");
        }

        [HttpGet]
        [Route("GetDataSource")]
        public string GetDataSource()
        {
            string userId = HttpContext.Session.GetString("UserId");
            using (var connection = new SqlConnection(connectionString))
            {
                string query = "Select * FROM \"" + userId + "\" where sharedBy=''";
                connection.Open();
                var command = new SqlCommand(query, connection);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                List<FileNameInfo> dataSource = new List<FileNameInfo>();
                int i = 0;
                foreach (DataRow row in table.Rows)
                {
                    FileNameInfo info = new FileNameInfo(i + 1, row["documentName"].ToString());
                    info.DocumentID = row["documentId"].ToString();
                    info.CreatedOn = row["createdOn"].ToString();
                    info.SharedWith = row["sharedWith"].ToString();
                    info.Owner = userId;
                    dataSource.Add(info);

                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(dataSource);
            }
            return "[]";
        }
        [HttpGet]
        [Route("GetSharedDataSource")]
        public string GetSharedDataSource()
        {
            string userId = HttpContext.Session.GetString("UserId");
            using (var connection = new SqlConnection(connectionString))
            {
                string query = "Select * FROM \"" + userId + "\" where sharedBy!=''";
                connection.Open();
                var command = new SqlCommand(query, connection);
                SqlDataReader reader = command.ExecuteReader();
                DataTable table = new DataTable();
                table.Load(reader);

                List<FileNameInfo> dataSource = new List<FileNameInfo>();
                int i = 0;
                foreach (DataRow row in table.Rows)
                {
                    FileNameInfo info = new FileNameInfo(i + 1, row["documentName"].ToString());
                    info.DocumentID = row["documentId"].ToString();
                    info.CreatedOn = row["createdOn"].ToString();
                    info.SharedWith = row["sharedWith"].ToString();
                    info.SharedBy = row["sharedBy"].ToString();
                    info.Owner = row["documentOwner"].ToString();
                    dataSource.Add(info);

                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(dataSource);
            }
            return "[]";
        }


        [HttpPost]
        [Route("ShareDocument")]
        public string ShareDocument(DocumentShareInfo info)
        {
            string userId = HttpContext.Session.GetString("UserId");

            using (var connection = new SqlConnection(connectionString))
            {
                string updateShare = "UPDATE \"" + userId + "\" set sharedWith=@ShareWith Where documentId='" + info.DocumentId + "'";
                connection.Open();
                var command = new SqlCommand(updateShare, connection);
                command.Parameters.Add("@ShareWith", SqlDbType.NVarChar).Value = info.SharedWith;
                command.ExecuteNonQuery();


                string[] users = info.SharedWith.Split(";");

                for (int i = 0; i < users.Length; i++)
                {
                    string updateSharedInfo = "Select * from user_info where email=@email";
                    var updateCommand = new SqlCommand(updateSharedInfo, connection);
                    updateCommand.Parameters.Add("@email", SqlDbType.NVarChar).Value = users[i];

                    SqlDataReader reader = updateCommand.ExecuteReader();
                    DataTable table = new DataTable();
                    table.Load(reader);
                    if (table.Rows.Count > 0)
                    {
                        string remoteUserId = table.Rows[0]["userId"].ToString();
                        CheckAndCreateTableForDocument(remoteUserId, info.DocumentName, null, HttpContext.Session.GetString("EmailId"), info.DocumentId, userId);
                    }
                    else
                    {
                        string newUserId = Regex.Replace(Guid.NewGuid().ToString(), "-", "");
                        string query = "INSERT INTO user_info (email, name, userId) " + "VALUES (@email, @name, @userId);";
                        var insertCommand = new SqlCommand(query, connection);
                        insertCommand.Parameters.Add("@email", SqlDbType.NVarChar).Value = users[i];
                        insertCommand.Parameters.Add("@name", SqlDbType.NVarChar).Value = "";
                        insertCommand.Parameters.Add("@userId", SqlDbType.NVarChar).Value = newUserId;
                        insertCommand.ExecuteNonQuery();
                        CheckAndCreateTableForDocument(newUserId, info.DocumentName, null, HttpContext.Session.GetString("EmailId"), info.DocumentId, userId);
                    }
                }
                return GetDataSource();
            }
            return "[]";
        }

    }


    public class DocumentShareInfo
    {
        public string DocumentId { get; set; }
        public string SharedWith { get; set; }

        public string DocumentName { get; set; }
    }

    public class FilesPathInfo
    {
        public string fileName { get; set; }
    }

    public class FileNameInfo
    {
        public int FileIndex { get; set; }
        public string FileName { get; set; }

        public string CreatedOn { get; set; }

        public string SharedWith { get; set; }

        public string DocumentID { get; set; }

        public string SharedBy { get; set; }

        public string Owner { get; set; }

        public List<string> ActiveUsers { get; set; } = new List<string> { };

        public FileNameInfo(int index, string fileName)
        {
            this.FileIndex = index;
            this.FileName = fileName;
            if (DocumentEditorHub.groupManager.ContainsKey(fileName))
            {
                List<ActionInfo> users = DocumentEditorHub.groupManager[fileName];
                for (int i = 0; i < users.Count; i++)
                {
                    ActiveUsers.Add(ConstructInitials(users[i].CurrentUser));
                }
            }
        }

        public string ConstructInitials(string authorName)
        {
            string[] splittedName = authorName.Split(' ');
            string initials = "";
            for (int i = 0; i < splittedName.Length; i++)
            {
                if (splittedName[i].Length > 0 && splittedName[i] != "")
                {
                    initials += splittedName[i][0];
                }
            }
            return initials;
        }
    }
}
