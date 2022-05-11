using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Data.SqlClient;
using System.Text;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace FeedMamaVerification.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        [BindProperty]
        public string FirstName { get; set; }

        [BindProperty]
        public string License { get; set; }

        [BindProperty]
        public string LastName { get; set; }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public int? Verified { get; set; }
        [BindProperty]
        public int? VerificationAttempts { get; set; }

        [BindProperty]
        public string UserID { get; set; }

        [BindProperty]
        public string? Error { get; set; }

        public int MyProperty { get; set; }

        [BindProperty]
        public string GenderSelection { get; set; }

        [BindProperty]
        public string YNSelection { get; set; }

        [BindProperty]
        public Physician Physician { get; set; }

        public void OnGet(string userID, string? error)
        {
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = "feedmamaserver.database.windows.net";
                builder.UserID = "admin@feedmama.org@feedmamaserver";
                builder.Password = "f33dm@m@777";
                builder.InitialCatalog = "feedmamadatabase";

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    string sql = "SELECT FirstName, LastName, Email, Verified, VerificationAttempts FROM dbo.Users WHERE UserID = '" + userID.ToString() + "';";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {

                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                FirstName = reader.GetString(0);
                                LastName = reader.GetString(1);
                                Email = reader.GetString(2);
                                try
                                {
                                    Verified = reader.GetInt32(3);
                                }
                                catch (Exception)
                                {
                                    Verified = 0;
                                }

                                try
                                {
                                    VerificationAttempts = reader.GetInt32(4);
                                }
                                catch (Exception)
                                {
                                    VerificationAttempts = 0;
                                }
                            }
                        }
                    }
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }

            UserID = userID;
            Error = error;

        }

        public ActionResult OnPost()
        {
            if(VerificationAttempts >= 3) //No more than 3 attempts
            {
                return RedirectToPage("./Index", new {userID = UserID, error = "Error: You have reached the limit of unsuccessful attempts" });
            }

            //Create Variables for updading database
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "feedmamaserver.database.windows.net";
            builder.UserID = "admin@feedmama.org@feedmamaserver";
            builder.Password = "f33dm@m@777";
            builder.InitialCatalog = "feedmamadatabase";


            using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {

                //Check if doctor denies pregnancy
                if (YNSelection.Equals("No") || YNSelection.Equals("Not Sure"))
                {

                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "Error: You have denied your patient is expecting a mother, if this was a mistake, please try again." }); //Doctor denied pregnancy, update database and return error
                }

                //Invalid NPI
                if (License.Length != 10)
                {
                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "Error: You entered an invalid NPI." }); //Invalid NPI
                }

                //Check against the type of doctor
                List<string> TypeList = new List<string>
                {
                    "Child & Adolescent Psychiatry",
                    "Complex Family Planning",
                    "Family Medicine",
                    "Female Pelvic Medicine and Reconstructive Surgery",
                    "Maternal-Fetal Medicine",
                    "Medical Oncology",
                    "Neonatal-Perinatal Medicine",
                    "Obstetrics & Gynecology",
                    "Pediatrics",
                    "Physician"
                };

                bool correctType = false;
                foreach (string type in TypeList)
                {
                    if (Physician.Type.Equals(type) || type.Contains(Physician.Type) || Physician.Type.Contains(type))
                    {
                        correctType = true;
                        break;
                    }
                }

                if (!correctType)
                {
                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "Error: A " + Physician.Type + " should not be verifying that their patient is pregant."  }); //Doctor type was not a doctor to verify pregnancy
                }

                int numResults = 0;
                List<string> FullName = new List<string>();
                List<string> reportedCity = new List<string>();
                List<string> reportedMI = new List<string>();
                var driver = new ChromeDriver();
                try
                {
                    driver.Navigate().GoToUrl("https://www.filterbypass.me/"); //Visit page to bypass controls on website
                    Thread.Sleep(1000);
                    driver.FindElement(By.XPath("/html/body/header/div[5]/div[1]/div[2]/div[1]/form/div/div[1]/div[1]/div/input")).SendKeys("https://www.certificationmatters.org/find-my-doctor/"); //Input Link for certifications
                    Thread.Sleep(1000);
                    var visitPage = driver.FindElement(By.XPath("/html/body/header/div[5]/div[1]/div[2]/div[1]/form/div/div[1]/div[2]/div[2]/div/button")); //Find submit button
                    visitPage.Click(); //Go to page
                    Thread.Sleep(1000);

                    driver.FindElement(By.XPath("/html/body/main/div[3]/section[4]/div/div/div/form/div[1]/ul/li[1]/input")).SendKeys(Physician.FirstName); //Input First Name
                    Thread.Sleep(1000);
                    driver.FindElement(By.XPath("/html/body/main/div[3]/section[4]/div/div/div/form/div[1]/ul/li[2]/input")).SendKeys(Physician.LastName); //Input Last Name
                    Thread.Sleep(1000);
                    var selectElement = new SelectElement(driver.FindElement(By.XPath("/html/body/main/div[3]/section[4]/div/div/div/form/div[1]/ul/li[3]/select"))); //Find DropDown
                    selectElement.SelectByText(Physician.State); //input State
                    Thread.Sleep(1000);
                    var findDoctor = driver.FindElement(By.XPath("/html/body/main/div[3]/section[4]/div/div/div/form/div[2]/button")); //Find submit button
                    findDoctor.Click(); //Click submit button
                    Thread.Sleep(1000);

                    var results = driver.FindElements(By.XPath("/html/body/main/div[3]/main/div/div/div/p/em")); //Find number of results found in search
                    foreach (var item in results)
                    {
                        numResults = Convert.ToInt32(item.Text.Substring(0, item.Text.IndexOf(" "))); //Gets the number of results as an integer. Adds to list in the case of multiple
                    }

                    results = driver.FindElements(By.XPath("/html/body/main/div[3]/main/div/div/div/table/tbody/tr/td[1]/a")); //Find full doctor name
                    foreach (var item in results)
                    {
                        FullName.Add(item.Text.Replace(" ", "").ToLower()); //Remove spaces and make lowercase, add to list of full names
                        reportedMI.Add(item.Text.Substring(item.Text.IndexOf(" "), item.Text.IndexOf(" ") + 2).ToLower()); //Grab middle initial, lowercase, add to list
                    }

                    results = driver.FindElements(By.XPath("/html/body/main/div[3]/main/div/div/div/table/tbody/tr/td[2]")); //Find last known town/city
                    foreach (var item in results)
                    {
                        reportedCity.Add(item.Text.Substring(0, item.Text.IndexOf(",")).Replace(" ", "").ToLower()); //Grab town/city name, remove spaces, lowercase
                    }
                    driver.Quit();
                }
                catch (Exception)
                {
                    driver.Quit();
                    return RedirectToPage("./Index", new { userID = UserID, error = "Error: Something went wrong on our end. Please try again." }); //Web driver failed
                }

                //Verify with found information
                if (numResults == 0)
                {
                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "We did not find any board certified doctors by that name. Did you type in the correct information?" }); //No doctor by that name was found to be board certified
                }

                //Checks the previously inputed full name against the "background check" with and without the middle initial.
                string VerifyName = (Physician.FirstName + Physician.MiddleInitial + Physician.LastName).ToLower();
                string VerifyNameNoMiddle = (Physician.FirstName + Physician.LastName).ToLower();
                bool nameIsVerified = false;
                if (FullName.Contains(VerifyName) || FullName.Contains(VerifyNameNoMiddle))
                {
                    nameIsVerified = true;
                }
                else
                {
                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "We did not find any board certified doctors by that name. Did you type in the correct information?" }); //Doctor Name with or without middle initial was not found
                }

                //Checks inputed town/city name against what was found
                string VerifyCity = Physician.City.Replace(" ", "").ToLower();
                bool cityIsVerified = false;
                if (reportedCity.Contains(VerifyCity))
                {
                    cityIsVerified = true;
                }
                else
                {
                    denialOfVerification(connection, Verified, VerificationAttempts);
                    return RedirectToPage("./Index", new { userID = UserID, error = "We did not find any board certified doctors practicing in that city. Did you type in the correct information?" }); //Cities found do not match
                }

                //Everything checks out - update mother's status in database
                if (nameIsVerified && cityIsVerified)
                {
                    SqlCommand cmd = new SqlCommand("UPDATE dbo.Users SET Verified = @VerParam WHERE UserID = '" + UserID.ToString() + "';", connection);
                    cmd.Parameters.Add("@VerParam", System.Data.SqlDbType.Int);
                    cmd.Parameters["@VerParam"].Value = 1;
                    try
                    {
                        connection.Open();
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        //SQL Failed
                    }
                    finally
                    {
                        connection.Close();
                    }
                }

                return RedirectToPage("./Index", new { userID = UserID, error = "You are verified!!" }); //Success!
            }
        }

        public void denialOfVerification(SqlConnection connection, int? Verified, int? VerificationAttempts)
        {
            SqlCommand cmd = new SqlCommand("UPDATE dbo.Users SET Verified = @VerParam, VerificationAttempts = @VerAttParam WHERE UserID = '" + UserID.ToString() + "';", connection);

            cmd.Parameters.Add("@VerParam", System.Data.SqlDbType.Int);
            cmd.Parameters["@VerParam"].Value = Verified;

            cmd.Parameters.Add("@VerAttParam", System.Data.SqlDbType.Int);
            cmd.Parameters["@VerAttParam"].Value = VerificationAttempts + 1;

            try
            {
                connection.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                //SQL Failed
            }
            finally
            {
                connection.Close();
            }
        }
    }
}