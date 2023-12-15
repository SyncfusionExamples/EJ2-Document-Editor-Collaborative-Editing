using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private string connectionString;
        public HomeController(IConfiguration config)
        {
            this.connectionString = config.GetConnectionString("DocumentEditorDatabase");
        }
        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
            {
                var result = Url.Action("LogIn", "Home");
                return Redirect(result);
            }

            ViewBag.currentUser = HttpContext.Session.GetString("UserName");
            return View();
        }

        public IActionResult LogIn(string id)
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")) &&
                !string.IsNullOrEmpty(HttpContext.Session.GetString("EmailId")) &&
                !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                var result = Url.Action("Index", "Home");
                return Redirect(result);
            }

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }

        public IActionResult Open(string id, string userName)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
            {
                if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
                {
                    //var result = Url.Action(,);
                    return Redirect("/Home/LogIn?returnUrl=/Home/Open/" + userName + "/" + id);
                }
            }
            string currentUser = HttpContext.Session.GetString("UserId");
            string emailId = HttpContext.Session.GetString("EmailId");
            if (currentUser != userName)
            {
                string query = "SELECT sharedWith from \"" + userName + "\" WHERE documentId='" + id + "'";
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(query, connection);
                    object sharedWidth = command.ExecuteScalar();
                    if (sharedWidth == null)
                    {
                        ViewBag.errorMessage = "Document Not found";
                        return View("Error");
                    }
                    else if (!sharedWidth.ToString().Contains(emailId))
                    {
                        ViewBag.errorMessage = "Access denied, you don't have permission to access this document";
                        return View("Error");
                    }
                }
            }

            ViewBag.currentUser = HttpContext.Session.GetString("UserName");
            ViewBag.fileName = id;
            ViewBag.userName = userName;
            return View();
        }


        [HttpPost]
        public IActionResult LogIn(string userName, string emailId, string returnUrl)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserName")))
            {
                HttpContext.Session.SetString("UserName", userName);
            }
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("EmailId")))
            {
                HttpContext.Session.SetString("EmailId", emailId);
            }
            HttpContext.Session.SetString("UserId", GetUserId(emailId, userName));

            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

        private string GetUserId(string emailId, string userName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                if (IsTableExist("user_info", connection))
                {
                    string guid = "Select userId from user_info where email=@email";
                    var command1 = new SqlCommand(guid, connection);
                    command1.Parameters.Add("@email", SqlDbType.NVarChar).Value = emailId;
                    var result = command1.ExecuteScalar();
                    if (result == null)
                    {
                        string userId = Regex.Replace(Guid.NewGuid().ToString(), "-", "");
                        string query = "INSERT INTO user_info (email, name, userId) " + "VALUES (@email, @name, @userId);";
                        var command = new SqlCommand(query, connection);
                        command.Parameters.Add("@email", SqlDbType.NVarChar).Value = emailId;
                        command.Parameters.Add("@name", SqlDbType.NVarChar).Value = userName;
                        command.Parameters.Add("@userId", SqlDbType.NVarChar).Value = userId;
                        command.ExecuteNonQuery();
                        return userId;
                    }
                    else
                    {
                        string nameId = "Select name from user_info where email=@email";
                        var command2 = new SqlCommand(nameId, connection);
                        command2.Parameters.Add("@email", SqlDbType.NVarChar).Value = emailId;
                        var result2 = command2.ExecuteScalar().ToString();
                        if (string.IsNullOrEmpty(result2))
                        {
                            string updateShare = "UPDATE user_info set name=@name Where email=@email";
                            var command3 = new SqlCommand(nameId, connection);
                            command3.Parameters.Add("@email", SqlDbType.NVarChar).Value = emailId;
                            command3.Parameters.Add("@name", SqlDbType.NVarChar).Value = userName;
                            command3.ExecuteNonQuery();
                        }
                        return result.ToString();
                    }
                }
            }
            return "";
        }
        private bool IsTableExist(string tableName, SqlConnection connection)
        {
            var command = new SqlCommand($"SELECT CASE WHEN OBJECT_ID('{tableName}', 'U') IS NOT NULL THEN 1 ELSE 0 END", connection);
            var result = (int)command.ExecuteScalar();
            if (result != 1)
            {
                string queryString = "CREATE TABLE user_info (email nvarchar(400) PRIMARY KEY,name nvarchar(max), userId nvarchar(200))";
                var command1 = new SqlCommand(queryString, connection);
                command1.ExecuteNonQuery();
                return true;
            }
            return true;
        }
    }
}
