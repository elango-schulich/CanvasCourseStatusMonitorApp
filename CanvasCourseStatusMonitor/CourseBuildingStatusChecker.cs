using canvasApiLib.API;
using Newtonsoft.Json.Linq;
using NLog;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//using System.Threading.Tasks;

namespace CourseStatusMonitorApp
{

    /// <summary>
    /// CourseBuildingStatusChecker - reads components of course status from Canvas API and Oracle DB and writes it to SQL server table 
    /// </summary>
	class CourseBuildingStatusChecker
    {
        ////NLog
        private static Logger _logger = LogManager.GetLogger(typeof(CourseBuildingStatusChecker).FullName);
        public bool intializationError = false;

        // Canvas Access Details
        private static readonly String apiAccessToken = "8296~iiMfDEH6Gqp1inhsGrUMAU9I5SQrwxEVXiw01CfHnkIJ23ROJkXVx9DtRe3eq4fa"; //Production
        private static readonly string apiUrl = "https://schulich.instructure.com"; //Production

        //config variables
        string oldDateString = "2015-01-01";
        string termStartDateString = "2020-08-05"; //fall 2020 start date minus 1 month
        string termEndDateString = "2020-12-31"; //fall 2020

        //sql server connection string
        private static string sqlServerConnectionString = "Data Source= dataviz.schulich.yorku.ca; Initial Catalog =CanvasCourseStatuses; " +
                "Persist Security Info = True; User ID = CanvasDataAdmin; Password = qGZxpxRrsz82gBYUf8q3";
        //private static string oracleConnectionString = "User Id=saturdaynight; Password=compaq1720; Data Source=130.63.69.32:1521/ssbcore; Pooling =false;";
        private static string oracleConnectionString = "User Id=elango; Password=okn5678; Data Source=130.63.69.34:1521/ssbcore; Pooling =false;";

        private static OracleConnection oracleDbConnection;
        private static SqlConnection sqlServerDbConnection;

        private CourseBuildingStatusFlags aCourseBuildingStatusFlags; //holds the status of one course
        private CourseDetailsFromAPI aCourseDetailsFromAPI;//holds the values from API for one course

        //List of all course ids
        private List<CourseIds> listOfAllCourseIds = new List<CourseIds>();

        public bool working = true;

        private enum LOG_LEVEL
        {
            DEBUG = 0,
            INFO,
            WARN,
            ERROR,
            EXCEPTION,
            FATAL
        }

        private class CourseIds
        {
            public string canvas_id { get; set; }
            public string sis_id { get; set; }
            public CourseIds(string canvasId, String sisId)
            {
                canvas_id = canvasId;
                sis_id = sisId;
            }
        }


        /* -------------------------------------------------
         * Constructor - get course building status for all courses in a specified term
         *
         * -------------------------------------------------
         */
        public CourseBuildingStatusChecker(String term)
        {
            setupDatabaseConnections();

            if (!intializationError)
            {
                //1. Get SIS ID and other metadata for each course from the current term from the Oracle Database View - CANVAS_COURSE_METADATA_VIEW   
                //   AND write to the SQL server database - CANVAS_COURSE_METADATA
                //getCourseMetdataFromOracleDb(term);
                //2. get all course ids from the database 
                getListOfAllCourseIdsFromDb(term);
                //3. get the course building status of all the courses and write to SQL server database - CANVAS_COURSE_BUILDING_STATUS
                getCourseBuildingStatus().GetAwaiter();
                while (working)
                {
                    Thread.Sleep(100);
                }
            }
        }


        /* -------------------------------------------------
         * TBD: Future extension
         * Respond to Live API module changes
         * get course bulding status for a specific  course 
         * -------------------------------------------------
         * /
        public CourseBuildingStatusChecker(String term, String course_sis_id)
        {
            setupDatabaseConnections();

            //TBD: 
            //1. Get SIS ID and other metadata for each course from the current term from the Oracle Database View - CANVAS_COURSE_METADATA_VIEW   
            if (!intializationError) getCourseMetdataFromOracleDb(course_sis_id);
            //2. get all course ids from the database and loop through them
            //getListOfAllCourseIdsFromDb();

        }
        */

        /* ----------------------------------------------------------------
         * setupDatabaseConnections()
         * 
         * The data base connections are already set up before checking course status tasks
         * ----------------------------------------------------------------
         */
        private void setupDatabaseConnections()
        {
            //1. Verfiy can connect to SQL server - update course status table
            bool sqlServerConnectionError = true;

            try
            {
                sqlServerDbConnection = new SqlConnection(sqlServerConnectionString);
                if (sqlServerDbConnection.Equals(null))
                {
                    LogMessage("Creating SQL Server connection failed.", LOG_LEVEL.FATAL, false);
                    sqlServerConnectionError = true;
                }
                else
                {
                    LogMessage("SQL server connection succeeded.", LOG_LEVEL.INFO, false);
                    sqlServerConnectionError = false;
                }

            }
            catch (Exception e)
            {
                LogMessage("Creating SQL server connection failed." + e.Message, LOG_LEVEL.EXCEPTION, true, e);
                sqlServerConnectionError = true;
            }


            //2. Verfiy can connect to Oracle DB
            bool oracleDbConnectionError = true;

            try
            {
                oracleDbConnection = new OracleConnection { ConnectionString = oracleConnectionString };
                if (oracleDbConnection.Equals(null))
                {
                    LogMessage("Creating Oracle DB connection failed.", LOG_LEVEL.FATAL, false);
                    oracleDbConnectionError = true;
                }
                else
                {
                    LogMessage("Oracle connection succeeded.", LOG_LEVEL.INFO, false);
                    oracleDbConnectionError = false;
                }

            }
            catch (Exception e)
            {
                LogMessage("Creating Oracle DB connection failed." + e.Message, LOG_LEVEL.EXCEPTION, true, e);
                oracleDbConnectionError = true;
            }

            if (oracleDbConnectionError || sqlServerConnectionError) intializationError = true; // Something went wrong!! ABORT ABORT

        }

        private void getCourseMetdataFromOracleDb(String term)
        {
            //1.Read all course metadata fromOracle DB view - Canvas_course_metadata_view
            //2. Write to SQL server - 
            //2a: If the course already exists update the information if any
            //2b: if the course doesn't exists crete a new record for the course
            Console.WriteLine("Entering getListOfAllCuorseIdsFromDb for term: " + term + "...");
            int numberOfCourses = 0;
            try
            {
                OracleDataReader reader;
                //string course_sis_id;


                using (OracleConnection oracleDbConnection = new OracleConnection { ConnectionString = oracleConnectionString })
                {
                    oracleDbConnection.Open();
                    OracleCommand command = oracleDbConnection.CreateCommand();
                    string sql = "SELECT course_sis_id, course_title, instructor_name, instructor_email, admin_name, admin_email, " +
                        "announce, director_office_hours, learning_outcomes, course_material, " +
                        "evaluation, calc_course_grade, preparation, class_participation " +
                        "FROM CANVAS_COURSE_METADATA_VIEW WHERE COURSE_SIS_ID LIKE '%" + term + "%'";
                    //Console.WriteLine(" SQL Query: " + sql);

                    command.CommandText = sql;
                    reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        numberOfCourses++;
                        if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["course_sis_id"])))
                        {
                            CourseMetadata aCourseMetadata = new CourseMetadata((string)reader["course_sis_id"]);
                            //Console.WriteLine(" Course SIS ID : " + aCourseMetadata.course_sis_id);
                            if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["course_title"])))
                                aCourseMetadata.course_title = (string)reader["course_title"];
                            //if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["program_initials"])))
                            //aCourseMetadata.program_initials = (string)reader["program_initials"];
                            if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["instructor_name"])))
                                aCourseMetadata.instructor_name = (string)reader["instructor_name"];
                            if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["instructor_email"])))
                                aCourseMetadata.instructor_email = (string)reader["instructor_email"];
                            if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["admin_name"])))
                                aCourseMetadata.admin_name = (string)reader["admin_name"];
                            if (!String.IsNullOrEmpty(getStringFromOracleReader(reader["admin_email"])))
                                aCourseMetadata.admin_email = (string)reader["admin_email"];

                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["announce"])))
                            {
                                aCourseMetadata.syllabus_notes_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections = "Notes\n";
                                LogMessage("Notes Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 19] SYL_OFFICE_HOURS_SECTION_PRESENT
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["director_office_hours"])))
                            {
                                aCourseMetadata.syllabus_office_hours_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Director Office Hours\n";
                                LogMessage("Office hours Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 20] SYL_LEARNING_OUTCOMES_SECTION_PRESENT
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["learning_outcomes"])))
                            {
                                aCourseMetadata.syllabus_learning_outcomes_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Learning Outcomes\n";
                                LogMessage("Learning Outcomes Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 21]
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["course_material"])))
                            {
                                aCourseMetadata.syllabus_course_materials_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Course Materials\n";
                                LogMessage("Course Materials Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 22]
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["evaluation"])))
                            {
                                aCourseMetadata.syllabus_written_assignements_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Evaluation\n";
                                LogMessage("Written assignments Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 23]
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["calc_course_grade"])))
                            {
                                aCourseMetadata.syllabus_grade_calculation_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Calculating Course Grades\n";
                                LogMessage("Grades calculation Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 24]
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["preparation"])))
                            {
                                aCourseMetadata.syllabus_preparation_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Preparation\n";
                                LogMessage("Preparation Section not present", LOG_LEVEL.DEBUG, false);
                            }

                            // >>>>>> [check# 25]
                            if (String.IsNullOrEmpty(getStringFromOracleReader(reader["class_participation"])))
                            {
                                aCourseMetadata.syllabus_class_participation_section_present = 0;
                                aCourseMetadata.syllabus_missing_sections += "Class Participation";
                                LogMessage("Class participation Section not present", LOG_LEVEL.DEBUG, false);
                            }
                            //LogMessage("Notes Section not present", LOG_LEVEL.DEBUG, true);
                            aCourseMetadata.CleanUpSingleQuotes();
                            writeCourseMetadataToDB(aCourseMetadata);
                            Console.WriteLine(numberOfCourses + " - Course Metadata Written into SQL server: " + reader["course_sis_id"]);
                        }
                    }
                    reader.Close();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(" Exception!" + e.ToString());

            }
            Console.WriteLine(" Total number of courses in the Oracle database: " + numberOfCourses);

        }

        /* WriteCourseMetadataToDB
         * Write the Meta data and Syallabus status to SQL server - Canvas_Course_MEtaData table
         */
        private bool writeCourseMetadataToDB(CourseMetadata aCourseMetadata)
        {


            using (sqlServerDbConnection = new SqlConnection(sqlServerConnectionString))
            {
                sqlServerDbConnection.Open();

                SqlCommand command = sqlServerDbConnection.CreateCommand();
                SqlTransaction transaction;


                // Start a local transaction.
                transaction = sqlServerDbConnection.BeginTransaction("SampleTransaction");

                // Must assign both transaction object and connection
                // to Command object for a pending local transaction
                command.Connection = sqlServerDbConnection;
                command.Transaction = transaction;

                try
                {
                    command.CommandText = "SELECT course_sis_id FROM canvas_course_metadata WHERE course_sis_id = '" + aCourseMetadata.course_sis_id + "'";
                    SqlDataReader reader = command.ExecuteReader();
                    bool courseMetadataExists = reader.HasRows;
                    reader.Close();


                    if (courseMetadataExists) //already status update from today exists
                    {

                        //Console.WriteLine(" Already course metadata exists.. Updating....");
                        //Update todays status for this course with new Time
                        String updateQuery = "UPDATE dbo.canvas_course_metadata  SET " +
                                              "course_sis_id = '" + aCourseMetadata.course_sis_id + "'," +
                                              "course_title = '" + aCourseMetadata.course_title + "'," +
                                              //"program_initials =' + aCourseMetadata.program_initials + "'," +
                                              "instructor_name = '" + aCourseMetadata.instructor_name + "'," +
                                              "instructor_email= '" + aCourseMetadata.instructor_email + "'," +
                                              "admin_name = '" + aCourseMetadata.admin_name + "'," +
                                              "admin_email = '" + aCourseMetadata.admin_email +
                                              "', syl_notes_section_present = '" + aCourseMetadata.syllabus_notes_section_present +
                                             "', syl_office_hours_section_present = '" + aCourseMetadata.syllabus_office_hours_section_present +
                                             "', syl_learning_outcomes_section_present = '" + aCourseMetadata.syllabus_learning_outcomes_section_present +
                                             "', syl_course_materials_section_present = '" + aCourseMetadata.syllabus_course_materials_section_present +
                                             "', syl_written_assignements_section_present = '" + aCourseMetadata.syllabus_written_assignements_section_present +
                                             "', syl_grade_calculation_section_present = '" + aCourseMetadata.syllabus_grade_calculation_section_present +
                                             "', syl_preparation_section_present = '" + aCourseMetadata.syllabus_preparation_section_present +
                                             "', syl_class_participation_section_present = '" + aCourseMetadata.syllabus_class_participation_section_present +
                                              "', syl_missing_sections = '" + aCourseMetadata.syllabus_missing_sections +
                                             "', time_of_update = '" + DateTime.Now.ToString() +
                                             "' WHERE  course_sis_id = '" + aCourseMetadata.course_sis_id + "'";
                        command.CommandText = updateQuery;
                        LogMessage("**************************************** UPDATE Query :" + updateQuery, LOG_LEVEL.DEBUG, false);
                        command.ExecuteNonQuery();

                    }
                    else  //first status update for today 
                    {

                        //Console.WriteLine(" First update of the day!");
                        //insert new row
                        String insertQuery = "INSERT INTO dbo.canvas_course_metadata (course_sis_id, " +
                                             "course_title, instructor_name, instructor_email, admin_name, admin_email, " +
                                             "syl_notes_section_present, syl_office_hours_section_present, syl_learning_outcomes_section_present, syl_course_materials_section_present," +
                                             "syl_written_assignements_section_present, syl_grade_calculation_section_present, syl_preparation_section_present, syl_class_participation_section_present, time_of_update) " +
                                             " VALUES ('" +
                                                        aCourseMetadata.course_sis_id + "','" +
                                                        aCourseMetadata.course_title + "','" +
                                                        //aCourseMetadata.program_initials + "," +
                                                        aCourseMetadata.instructor_name + "','" +
                                                        aCourseMetadata.instructor_email + "','" +
                                                        aCourseMetadata.admin_name + "','" +
                                                        aCourseMetadata.admin_email + "','" +
                                                        aCourseMetadata.syllabus_notes_section_present + "','" +
                                                        aCourseMetadata.syllabus_office_hours_section_present + "','" +
                                                        aCourseMetadata.syllabus_learning_outcomes_section_present + "','" +
                                                        aCourseMetadata.syllabus_course_materials_section_present + "','" +
                                                        aCourseMetadata.syllabus_written_assignements_section_present + "','" +
                                                        aCourseMetadata.syllabus_grade_calculation_section_present + "','" +
                                                        aCourseMetadata.syllabus_preparation_section_present + "','" +
                                                        aCourseMetadata.syllabus_class_participation_section_present + "','" +
                                                        DateTime.Now.ToString() +
                                                     "')";

                        command.CommandText = insertQuery;
                        LogMessage("**************************************** Insert Query :" + insertQuery, LOG_LEVEL.DEBUG, false);
                        command.ExecuteNonQuery();

                    }
                    // Attempt to commit the transaction.
                    transaction.Commit();
                    LogMessage("Course status written to database. \n**************", LOG_LEVEL.INFO, false);
                    sqlServerDbConnection.Close();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Commit Exception Type: {0}", e.GetType());
                    //Console.WriteLine("  Message: {0}", e.Message);
                    LogMessage(" Exception " + e.ToString(), LOG_LEVEL.EXCEPTION, true, e);

                    // Attempt to roll back the transaction.
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception e2)
                    {
                        // This catch block will handle any errors that may have occurred
                        // on the server that would cause the rollback to fail, such as
                        // a closed connection.
                        //Console.WriteLine("Rollback Exception Type: {0}", e2.GetType());
                        //Console.WriteLine("  Message: {0}", e2.Message);
                        LogMessage(" Exception " + e2.ToString(), LOG_LEVEL.EXCEPTION, true, e2);
                    }
                    sqlServerDbConnection.Close();
                    return false;
                }
            }
        }


        private void getListOfAllCourseIdsFromDb(string term)
        {

            //1. Read all course information the list of courses from Oracle DB view - Canvas_courses_s2020_view
            //2. Write to SQL server - 
            //2a: If the course already exists update the information if any
            //2b: if the course doesn't exists crete a new record for the course

            //Console.WriteLine("Entering getListOfAllCuorseIdsFromDb ...");
            try
            {
                using (sqlServerDbConnection = new SqlConnection(sqlServerConnectionString))
                {

                    sqlServerDbConnection.Open();
                    //SqlCommand command = new SqlCommand("SELECT course_canvas_id, course_sis_id FROM dbo.Fall2019_course_details;", sqlServerDbConnection);
                    //W2020
                    SqlCommand command = new SqlCommand("SELECT course_sis_id FROM dbo.canvas_course_metadata " +
                        "WHERE course_sis_id LIKE '%" + term + "%' ;", sqlServerDbConnection);
                    //new SqlCommand("SELECT course_canvas_id FROM dbo.Fall2019_course_status;", sqlServerDbConnection);


                    SqlDataReader reader = command.ExecuteReader();
                    int courseCount = 0;
                    String canvasId, sisId;
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            canvasId = "";
                            sisId = reader.GetString(0).Trim();
                            //LogMessage("Getting course details - Canvas Id: " + canvasId + " SIS Id: " + sisId, LOG_LEVEL.DEBUG, true);
                            listOfAllCourseIds.Add(new CourseIds(canvasId, sisId));
                            courseCount++;
                        }
                    }
                    else
                    {
                        LogMessage("No course ids found in the database.", LOG_LEVEL.DEBUG, true);
                    }
                    LogMessage("Total number of courses read from DB: " + courseCount, LOG_LEVEL.INFO, true);

                    reader.Close();

                    sqlServerDbConnection.Close();
                }
            }
            catch (Exception e)
            {
                LogMessage("Retrieving course list from DB failed!" + e.Message, LOG_LEVEL.EXCEPTION, true, e);
                LogMessage("Retrieving course list from DB failed!", LOG_LEVEL.FATAL, true, e);
                sqlServerDbConnection.Close();
            }

        }




        /* ------------------------------------------------------------------
         *  getCourseBuildingStatus()
         *  
         *  The main method that gets the status through API and invokes the methods to update the course status database
         *  ------------------------------------------------------------------
         */
        public async Task<string> getCourseBuildingStatus()
        {
            //String courseSISId = "ACTG2010A_F2019_F";
            String courseSISId, courseCanvasId;
            int courseCount = 0;

            //-----------------------------------------------------------------
            // Iterate through the course list
            //-----------------------------------------------------------------
            int iteration = 0;
            bool testing = false;
            testing = true;
            foreach (CourseIds courseId in listOfAllCourseIds)
            {

                //Checking the status of this course
                courseCanvasId = courseId.canvas_id;
                courseSISId = courseId.sis_id;


                //******************************************************
                //*******************   TESTING   ********************** 
                //******************************************************
                if (testing)
                {
                    LogMessage("\n\n ********\n  TESTING IN PROGRESS \n ************", LOG_LEVEL.INFO, true);

                    //courseCanvasId = "3274";
                    //courseSISId = "MSTM5210M_S2020_S";

                    //courseCanvasId = "3468";
                    //courseSISId = "ACTG6801Q_S2020_Q5";

                    //courseCanvasId = "3409";
                    //courseSISId = "ACTG6160H_S2020_SA";COURSE_SIS_ID	COURSE_CANVAS_ID

                    courseCanvasId = "4091";
                    courseSISId = "SOCM6400U_F2020_F2";

                    iteration++;
                    if (iteration > 1)
                    {
                        working = false;
                        return "complete";
                    }
                    //break;
                }




                LogMessage("\n\n ********\n  Processing Course no:" + ++courseCount + " Id: (" + courseCanvasId + ") \n ************", LOG_LEVEL.INFO, true);
                //Console.WriteLine("\n\n ********\n  Processing Course no:" + ++courseCount + " Id: (" + courseCanvasId + ") \n ************");

                //int numberOfAssignments, numberOfModules, numberOfModuleItems, numberOfContentIds, numberOfAssignmentGroups, numberOfPages = 0;
                bool examModule, moduleContainsOverview;
                DateTime termStartDate = DateTime.ParseExact(termStartDateString, "yyyy-MM-dd", null);
                DateTime termEndDate = DateTime.ParseExact(termEndDateString, "yyyy-MM-dd", null);
                DateTime assignmentDueDate;

                List<string> groupsAssignmentDescriptionLiterals = new List<string>(new string[] { "Group", "group", "team", "Team" });

                //Initiallize objects that hold the status values for a single course 
                aCourseDetailsFromAPI = new CourseDetailsFromAPI(courseCanvasId, courseSISId);
                //CourseStatusCheckResults - object that holds all the flags
                //Philosophy - Assume everything is ok until you find evidence to the contrary 
                //Default values for flags are always positive until we see some evidence to set it to negative
                aCourseBuildingStatusFlags = new CourseBuildingStatusFlags(courseCanvasId, courseSISId);

                /* **************     List of Checks    ******************
                    
                    //Basic Checks - exists and not empty or shell
                    1. COURSE_EXISTS 
                    2. COURSE_PUBLISHED
                    
                    BASIC_CHECK_PASSED
                    BASIC_CHECK_EXCPETIONS


                    //Announcements
                    3. ANNOUNCEMENTS_VALID_DATES
                    
                    ANNOUNCEMENTS_CHECK_PASSED
                    ANNOUNCEMENTS_CHECK_EXCEPTION
                
                
                    //Modules
                    4. MODULES_PRESENT
                    5. NO_SHELL_MODULES
                    6. ALL_MODULES_PUBLISHED
                    7. NO_EMPTY_MODULES
                    8. ALL_MODULE_ITEMS_PUBLISHED
                    9. NONEXAM_MODULES_OVERVIEW_PRESENT
                    
                    MODULES_CHECK_PASSED
                    MODULES_CHECK_EXCEPTIONS


                    //Pages
		            10. NO_SHELL_PAGES


                    //Assignments
                    11. ASSIGNMENTS_PRESENT
                    12. NO_SHELL_ASSIGNMENT_GROUPS
                    13. ASSIGNMENT_GROUPS_GRADES_TOTAL_100
                    14. ALL_ASSIGNMENTS_ADDED_TO_MODULES
                    15. NO_SHELL_ASSIGNMENTS
                    16. ASSIGNMENTS_VALID_DATES
                    17. ASSIGNMENTS_GROUP_DESIGNATION_DONE
                    
                    ASSIGNMENTS_CHECK_PASSED
                    ASSIGNMENTS_CHECK_EXCEPTIONS


                    //Syllabus
                    18. SYL_NOTES_SECTION_PRESENT
                    19. SYL_OFFICE_HOURS_SECTION_PRESENT
                    20. SYL_LEARNING_OUTCOMES_SECTION_PRESENT
                    21. SYL_COURSE_MATERIALS_SECTION_PRESENT
                    22. SYL_WRITTEN_ASSIGNEMENTS_SECTION_PRESENT
                    23. SYL_GRADE_CALCULATION_SECTION_PRESENT
                    24. SYL_PREPARATION_SECTION_PRESENT
                    25. SYL_CLASS_PARTICIPATION_SECTION_PRESENT
                    
                    SYLLABUS_CHECK_PASSED
                    SYLLABUS_CHECK_EXCEPTIONS
                    
                    STATUS_CHECK_COMPLETE 
                
                 */

                //string assignmentName, assignmentDescription, assignmentDueAt, assignmentGroupCategoryId; 

                try
                {

                    //************************
                    // A. General Checks
                    //************************
                    //**********************************************
                    // 1. Check if course exists on Canvas
                    // 2. check if the course is published
                    //***********************************************

                    //------ API Call 1  - Get Course Details using SIS UD
                    dynamic courseDetailsJson = await clsCoursesApi.getCourseDetailsBySisId(apiAccessToken, apiUrl, courseSISId);

                    // Check if the canvas Course ID has changed 
                    //Get course canvas id
                    courseCanvasId = (string)courseDetailsJson.SelectToken("$.id");

                    //Course Reset?
                    //At first, check if the course has been reset 
                    //Canvas ID for the cousrse has changed because of reset!
                    //if (!courseCanvasIdFromAPI.Trim().Equals(courseCanvasId.Trim())){ 

                    aCourseDetailsFromAPI.course_canvas_id = courseCanvasId.Trim();
                    aCourseBuildingStatusFlags.course_exists = 1;  // ++++++++++++++++++++ STATUS FLAG
                    aCourseBuildingStatusFlags.course_canvas_id = courseCanvasId.Trim(); //update with the new valule
                    //LogMessage("Course (" + courseCanvasId + ") has new canvas ID!", LOG_LEVEL.INFO, true);

                    //Update the database table with new course canvas id
                    /*
                    if (updateCourseCanvasIdInDB(courseSISId, courseCanvasId))
                        {
                            LogMessage(" SUCCCESS: DB update new Course Canvas Id (" + courseCanvasId + ") for (" + courseSISId + ").", LOG_LEVEL.DEBUG, true);
                        }
                        else
                        {
                            LogMessage(" FAILED: DB update new Course Canvas Id (" + courseCanvasId + ") for (" + courseSISId + ").", LOG_LEVEL.DEBUG, true);
                        };
                    */                    //}

                    //>>>>>> >>>>>> [check## 1] - Course exists (else exception would have happened) 
                    aCourseBuildingStatusFlags.course_exists = 1;  // ++++++++++++++++++++ STATUS FLAG


                    //>>>>>> >>>>>> [check## 2] - Course is published
                    if (!((string)courseDetailsJson.SelectToken("$..workflow_state")).Contains("available"))
                    {
                        aCourseBuildingStatusFlags.course_published = 0;  // ++++++++++++++++++++ STATUS FLAG
                        //aCourseBuildingStatusFlags.potential_errors += "Course not published\n";// ++++++++++++++++++++ ERROR MESSAGE
                        aCourseBuildingStatusFlags.general_section_incomplete += ";\"Course is not yet published\"";
                    }
                    else
                    {
                        aCourseBuildingStatusFlags.general_section_complete += ";\"Course is published\"";

                    }
                    //Console.WriteLine(jsonCourseDetails);

                    LogMessage("Received course details results: SUCCESS", LOG_LEVEL.DEBUG, true);
                    LogMessage("****************************************", LOG_LEVEL.DEBUG, true);
                }
                catch (System.Net.Http.HttpRequestException hre) //course doesn't exist on canvas?
                {
                    LogMessage("\n\n *******************\n Exception - Course (" + courseSISId + ") does not exist on Canvas! \n" + hre.ToString(), LOG_LEVEL.EXCEPTION, true, hre);
                    aCourseBuildingStatusFlags.course_exists = 0; // ++++++++++++++++++++ STATUS FLAG
                    // ++++++++++++++++++++ ERROR MESSAGE
                    aCourseBuildingStatusFlags.potential_errors += "Course does not exist on Canvas!\n";// ++++++++++++++++++++ ERROR MESSAGE
                    aCourseBuildingStatusFlags.setAllValuesToFalse(); // ++++++++++++++++++++ STATUS FLAG
                    aCourseBuildingStatusFlags.skipped_checks = 0; // ++++++++++++++++++++ STATUS FLAG  
                    writeCourseBuildingStatusToDB(aCourseBuildingStatusFlags);//write course status to DB
                    continue;
                }

                //***********************
                // Announcements
                //***********************
                try
                {
                    dynamic announcementsData = await clsPagesAndAnnoucementsApi.listCourseAnnouncements(apiAccessToken, apiUrl, courseCanvasId,
                        oldDateString, termStartDateString);
                    IEnumerable<JToken> announcementIdTokens = announcementsData.SelectTokens("$..id");
                    IEnumerable<JToken> postedAtTokens = announcementsData.SelectTokens("$..created_at");
                    IEnumerable<JToken> titleTokens = announcementsData.SelectTokens("$..title");
                    DateTime announcementPostedAt;
                    string announcementId, announcementTitle, announcementErrorMessage, announcementURLSuffix;
                    bool oldAnnouncements = false;


                    //>>>>>> >>>>>> [check## 3] ANNOUNCEMENTS_VALID_DATES
                    aCourseBuildingStatusFlags.announcements_valid_dates = 0; // ++++++++++++++++++++ STATUS FLAG
                    //Console.WriteLine("number of old announcements: " + announcementIdTokens.Count() + " Announcements" + announcementsData);
                    for (int i = 0; i < titleTokens.Count(); i++)
                    {

                        announcementId = (string)announcementIdTokens.ElementAt(i);
                        announcementTitle = (string)titleTokens.ElementAt(i);
                        announcementPostedAt = DateTime.Parse((string)postedAtTokens.ElementAt(i));
                        //Console.WriteLine("Announcement id: " + announcementId + " Title: " + announcementTitle + " created date: " + announcementPostedAt);
                        //aCourseDetailsFromAPI.listOfAnnouncements.Add(new CourseDetailsFromAPI.Announcement((string)announcementIdTokens.ElementAt(i))); 
                        if (announcementPostedAt < termStartDate) //old announcement - error
                        {
                            oldAnnouncements = true;
                            announcementErrorMessage = "Announcement posted before " + termStartDate;
                            announcementURLSuffix = "discussion_topics/" + announcementId;
                            //Console.WriteLine(" URL: " + announcementURL + " Error Message: " + announcementErrorMessage);
                            aCourseBuildingStatusFlags.announcements_section_incomplete += ";\"" + announcementTitle + "\"," +
                                                                                          "\"" + announcementErrorMessage + "\"," +
                                                                                           "\"" + announcementURLSuffix + "\"";

                        }
                        else
                        {
                            aCourseBuildingStatusFlags.announcements_section_complete += ";\"" + announcementTitle + "\"";

                        }

                    }
                    // Summary of results for the General section 
                    if (oldAnnouncements)
                    {
                        aCourseBuildingStatusFlags.general_section_incomplete += ";\" Announcements older than " + termStartDateString + " are present\"";
                        aCourseBuildingStatusFlags.potential_errors += "Announcements posted before " + termStartDateString + "\n";//++++++++++++++++++++ ERROR MESSAGE
                    }
                    else
                    {
                        aCourseBuildingStatusFlags.general_section_complete += ";\" All announcements are current\"";

                    }
                    LogMessage("Received announcements results: SUCCESS", LOG_LEVEL.DEBUG, true);
                    LogMessage("****************************************", LOG_LEVEL.DEBUG, true);
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    LogMessage("\n\n *******************\n Exception - Course (" + courseCanvasId + ")." + e.ToString(), LOG_LEVEL.EXCEPTION, true, e);
                    Console.WriteLine("\n\n *******************\n Exception Course: {0} \n ************", courseCanvasId);
                    // ++++++++++++++++++++ ERROR MESSAGE
                }


                //***********************
                // C. Modules
                //***********************

                try
                {
                    //***********************
                    // C.1 Modules
                    //***********************
                    //------ API Call - Get the list of Modules in this course
                    dynamic modulesJson = await clsModulesApi.listCourseModules(apiAccessToken, apiUrl, courseCanvasId);
                    IEnumerable<JToken> moduleIdTokens = modulesJson.SelectTokens("$..id"); //get all module ids
                    aCourseDetailsFromAPI.numberOfModules = moduleIdTokens.Count();
                    string moduleId, moduleTitle, moduleErrorMessage, moduleURLSuffix;
                    bool modulePublished;
                    //Console.WriteLine(modulesJson);

                    // >>>>>> [check# 4] MODULES_PRESENT 
                    if (aCourseDetailsFromAPI.numberOfModules == 0) //Course is empty! 
                    {
                        aCourseBuildingStatusFlags.modules_present = 0; // ++++++++++++++++++++ STATUS FLAG
                        aCourseBuildingStatusFlags.potential_errors += "No modules\n";// ++++++++++++++++++++ ERROR MESSAGE
                        aCourseBuildingStatusFlags.general_section_incomplete += ";\"Course has no modules\"";
                    }
                    else //course not empty - proceed with further checks
                    {
                        aCourseBuildingStatusFlags.modules_present = 1; // ++++++++++++++++++++ STATUS FLAG
                        IEnumerable<JToken> moduleNameTokens = modulesJson.SelectTokens("$..name"); //get all module names
                        IEnumerable<JToken> modulePublishedTokens = modulesJson.SelectTokens("$..published"); //get all module published status
                                                                                                              //Console.WriteLine(Module );
                        bool unpublishedModulesPresent = false;
                        bool shellModulesPresent = false;
                        bool unpublishedModule = false;
                        bool shellModule = false;
                        bool emptyModulesPresent = false;
                        bool unpublishedModuleItemsPresent = false;
                        bool modulesWithoutOverviewPresent = false;

                        for (int i = 0; i < aCourseDetailsFromAPI.numberOfModules; i++) //iterate through the modules
                        {
                            unpublishedModule = false;
                            shellModule = false;

                            moduleId = (string)moduleIdTokens.ElementAt(i);
                            moduleTitle = (string)moduleNameTokens.ElementAt(i);
                            modulePublished = (bool)modulePublishedTokens.ElementAt(i);
                            moduleURLSuffix = "modules/" + moduleId;
                            aCourseDetailsFromAPI.listOfModules.Add(new CourseDetailsFromAPI.Module(moduleId));  // the ith module to module list
                            aCourseDetailsFromAPI.listOfModules.ElementAt(i).name = moduleTitle; // add the name to the module
                            aCourseDetailsFromAPI.listOfModules.ElementAt(i).published = modulePublished;

                            // >>>>>>>>>>>> [check# 5] No Shell Modules 

                            if ((moduleTitle.Contains("-Topic")) ||
                                (moduleTitle.Contains("- Topic")))
                            {
                                aCourseBuildingStatusFlags.no_shell_modules = 0; // ++++++++++++++++++++ STATUS FLAG
                                shellModulesPresent = true; // general error
                                shellModule = true; // module psecific error
                                moduleErrorMessage = "Shell module to be removed";
                                aCourseBuildingStatusFlags.modules_section_incomplete += ";\"" + moduleTitle + "\"," +
                                                                                         "\"" + moduleErrorMessage + "\"," +
                                                                                          "\"" + moduleURLSuffix + "\"";
                            }
                            // >>>>>> [check# 6] All Modules Published
                            if (!aCourseDetailsFromAPI.listOfModules.ElementAt(i).published)
                            {
                                aCourseBuildingStatusFlags.all_modules_published = 0;
                                unpublishedModulesPresent = true;// ++++++++++++++++++++ ERROR MESSAGE
                                unpublishedModule = true;// ++++++++++++++++++++ ERROR MESSAGE
                                moduleErrorMessage = "Module not published";
                                aCourseBuildingStatusFlags.modules_section_incomplete += ";\"" + moduleTitle + "\"," +
                                                                                         "\"" + moduleErrorMessage + "\"," +
                                                                                          "\"" + moduleURLSuffix + "\"";

                            }

                            //***********************
                            // C.2 Module Items
                            //***********************

                            bool emptyModule = false;
                            bool unpublishedModuleItem = false;
                            bool moduleWithoutOverview = false;

                            aCourseDetailsFromAPI.numberOfModuleItems = 0;
                            emptyModule = false;
                            unpublishedModuleItem = false;
                            moduleWithoutOverview = false;

                            //Console.WriteLine("\n\n -----------------");
                            //Console.WriteLine(" Module Id: " + module.id + " Name: " + module.name);
                            //Now get all the Modules Items for the ith Module
                            var modulesItemsJson = await clsModulesApi.listModuleItems(apiAccessToken, apiUrl, courseCanvasId, moduleId);
                            //Console.WriteLine("Module items : " + modulesItemsJson);
                            IEnumerable<JToken> moduleItemIdTokens = modulesItemsJson.SelectTokens("$..id");
                            aCourseDetailsFromAPI.numberOfModuleItems = moduleItemIdTokens.Count();

                            // >>>>>> [check# 7] Not an Empty Modules
                            if (aCourseDetailsFromAPI.numberOfModuleItems == 0)
                            {
                                //no module items in this module!
                                aCourseBuildingStatusFlags.no_empty_modules = 0; // ++++++++++++++++++++ STATUS FLAG
                                emptyModulesPresent = true;
                                emptyModule = true;
                                moduleErrorMessage = "Module is empty";
                                aCourseBuildingStatusFlags.modules_section_incomplete += ";\"" + moduleTitle + "\"," +
                                                                                         "\"" + moduleErrorMessage + "\"," +
                                                                                          "\"" + moduleURLSuffix + "\"";
                            }
                            else
                            {
                                aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems = new List<CourseDetailsFromAPI.ModuleItem>();
                                //initialize the list of module itemes for this module
                                //Console.WriteLine("Number of module items " + numberOfModuleItems);
                                //Console.WriteLine("moduleItemIdTokens: " + moduleItemIdTokens.Count());
                                IEnumerable<JToken> moduleItemTitleTokens = modulesItemsJson.SelectTokens("$..title");
                                //Console.WriteLine("moduleItemTitleTokens: " + moduleItemTitleTokens.Count());
                                IEnumerable<JToken> moduleItemPublishedTokens = modulesItemsJson.SelectTokens("$..published");
                                //Console.WriteLine("moduleItemPublishedTokens: " + moduleItemPublishedTokens.Count());

                                moduleContainsOverview = false;
                                if (aCourseDetailsFromAPI.listOfModules.ElementAt(i).name.Contains("xam")) //determin this module is a "exam module", if so doesn't need overview item
                                    examModule = true;
                                else examModule = false;

                                string moduleItemId, moduleItemTitle, moduleItemURLSuffix;

                                for (int j = 0; j < aCourseDetailsFromAPI.numberOfModuleItems; j++) //Iterating through all module items in this module
                                {
                                    moduleItemId = (string)moduleItemIdTokens.ElementAt(j);
                                    moduleItemTitle = (string)moduleItemTitleTokens.ElementAt(j);
                                    moduleItemURLSuffix = "modules/" + moduleId + "/items/" + moduleItemId;
                                    aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems.Add(new CourseDetailsFromAPI.ModuleItem(moduleItemId));
                                    aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems.ElementAt(j).title = moduleItemTitle;
                                    if (!examModule)
                                    {
                                        if (aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems.ElementAt(j).title.Contains("verview"))
                                            moduleContainsOverview = true; //at least one module item with overview in title 

                                    }
                                    aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems.ElementAt(j).published = (bool)moduleItemPublishedTokens.ElementAt(j);

                                    // >>>>>> [check# 8] All Module Items Published 
                                    if (!aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfModuleItems.ElementAt(j).published)
                                    {
                                        aCourseBuildingStatusFlags.all_module_items_published = 0; // ++++++++++++++++++++ STATUS FLAG
                                        unpublishedModuleItem = true; // ++++++++++++++++++++ ERROR MESSAGE
                                        unpublishedModuleItemsPresent = true;
                                        moduleErrorMessage = "Module item not published";
                                        aCourseBuildingStatusFlags.modules_section_incomplete += ";\"" + moduleItemTitle + "\"," +
                                                                                        "\"" + moduleErrorMessage + "\"," +
                                                                                         "\"" + moduleItemURLSuffix + "\"";
                                    }
                                    //Console.WriteLine(" Module Item Id: " + module.listOfModuleItems.ElementAt(i).id +
                                    //    " Title: " + module.listOfModuleItems.ElementAt(i).title +
                                    //    " published:" + module.listOfModuleItems.ElementAt(i).published);
                                    //Console.WriteLine(module.listOfModuleItems.ElementAt(i));
                                }

                                // >>>>>> [check# 9] Non Exam Modules Overview Present
                                if ((!examModule) && (!moduleContainsOverview))
                                {
                                    aCourseBuildingStatusFlags.nonexam_modules_overview_present = 0; // ++++++++++++++++++++ STATUS FLAG
                                    moduleWithoutOverview = true; // ++++++++++++++++++++ ERROR MESSAGE
                                    moduleErrorMessage = "Module does not have a ovrview item";
                                    aCourseBuildingStatusFlags.modules_section_incomplete += ";\"" + moduleTitle + "\"," +
                                                                                        "\"" + moduleErrorMessage + "\"," +
                                                                                         "\"" + moduleURLSuffix + "\"";
                                }
                            }


                            if (!(shellModule || unpublishedModule || emptyModule || moduleWithoutOverview || unpublishedModuleItem)) //module is complete
                            {
                                aCourseBuildingStatusFlags.modules_section_complete += ";\"" + moduleTitle + "\"";
                            }
                            //++++++++++++++++++++ STATUS FLAG
                            //Console.WriteLine("module Id: " + listOfModules.ElementAt(i).id);
                            //Console.WriteLine("module Name: " + listOfModules.ElementAt(i).name);


                            // Some additional work that will be useful when checking assignments
                            // Get content IDs for checking whether assignments are attached to the module
                            IEnumerable<JToken> moduleItemContentIdTokens = modulesItemsJson.SelectTokens("$..content_id");
                            aCourseDetailsFromAPI.numberOfContentIds = moduleItemContentIdTokens.Count();
                            //Console.WriteLine("Number of Content Ids in this module: " + numberOfContentIds);
                            if (aCourseDetailsFromAPI.numberOfContentIds != 0)
                            {
                                //Console.WriteLine("adding Content Ids to list in this module... ");
                                aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfContentIds = new List<string>(); //initialize the list of content ids for this module
                                for (int k = 0; k < aCourseDetailsFromAPI.numberOfContentIds; k++)
                                {
                                    aCourseDetailsFromAPI.listOfModules.ElementAt(i).listOfContentIds.Add((string)moduleItemContentIdTokens.ElementAt(k));
                                    //Console.WriteLine(" content id:" + module.listOfContentIds.ElementAt(i));
                                }

                            }




                        }

                        //End iterating through all modules - for loop

                        //General section error messages regarding modules
                        if (emptyModulesPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Empty module(s)\n";// ++++++++++++++++++++ ERROR MESSAGE
                        }
                        else
                        {
                            // no need for any message 
                        }

                        if (shellModulesPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Shell module(s)\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Shell modules to be removed\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"Shell modules have been removed\"";
                        }

                        if (modulesWithoutOverviewPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Module(s) without overview\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Modules without overview are present\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"All modules have overview\"";
                        }

                        if (unpublishedModulesPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Unpublished module(s)\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Unpubslihed modules are present\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"All modules have been publsihed\"";
                        }

                        if (unpublishedModuleItemsPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Module(s) with unpublished item(s)\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Unpubslihed module items are present\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"All modules have been pubslihed\"";
                        }

                        
                    }

                    //Console.WriteLine(jsonModules);
                    LogMessage("Received modules results: SUCCESS", LOG_LEVEL.DEBUG, true);
                    LogMessage("****************************************", LOG_LEVEL.DEBUG, true);
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    LogMessage("\n\n *******************\n Exception - Course (" + courseCanvasId + ")." + e.ToString(), LOG_LEVEL.EXCEPTION, true, e);
                    Console.WriteLine("\n\n *******************\n Exception Course: {0} \n ************", courseCanvasId);
                }




                //***********************
                // D. Pages
                //***********************

                try
                {
                    dynamic pagesJson = await clsPagesAndAnnoucementsApi.listCoursePages(apiAccessToken, apiUrl, courseCanvasId);
                    //Console.WriteLine(" Pages: " + pagesJson);
                    IEnumerable<JToken> pageIdTokens = pagesJson.SelectTokens("$..page_id");
                    aCourseDetailsFromAPI.numberOfPages = pageIdTokens.Count();
                    IEnumerable<JToken> pageTitleTokens = pagesJson.SelectTokens("$..title");
                    string pageId, pageTitle, pageErrorMessage, pageURLSuffix;
                    bool has_shell_pages = false;
                    for (int i = 0; i < aCourseDetailsFromAPI.numberOfPages; i++)
                    {
                        aCourseDetailsFromAPI.listOfPages.Add(new CourseDetailsFromAPI.Page((string)pageIdTokens.ElementAt(i)));
                        aCourseDetailsFromAPI.listOfPages.ElementAt(i).title = (string)pageTitleTokens.ElementAt(i);
                        //Console.WriteLine("Page #" + i + " title" + listOfPages.ElementAt(i).title);

                        // >>>>>> [check# 10]  No SHELL PAGES
                        // Checking if there are any shell pages?
                        pageId = aCourseDetailsFromAPI.listOfPages.ElementAt(i).id;
                        pageTitle = aCourseDetailsFromAPI.listOfPages.ElementAt(i).title;
                        pageErrorMessage = "Shell page to be removed";
                        pageURLSuffix = "pages/" + pageId;
                        if ((pageTitle.Contains("Overview: [")) && (aCourseDetailsFromAPI.IsShellPageAttachedToAModule(pageId)))
                        {

                            LogMessage("THE SHELL PAGE IS ATTACHED TO A MODULE. NOT OK. Page ID -> " + aCourseDetailsFromAPI.listOfPages.ElementAt(i).id + " Title: " +
                                    aCourseDetailsFromAPI.listOfPages.ElementAt(i).title, LOG_LEVEL.DEBUG, true);


                            //Console.WriteLine(" URL: " + announcementURL + " Error Message: " + announcementErrorMessage);
                            aCourseBuildingStatusFlags.pages_section_incomplete += ";\"" + pageTitle + "\"," +
                                                                                          "\"" + pageErrorMessage + "\"," +
                                                                                           "\"" + pageURLSuffix + "\"";
                            aCourseBuildingStatusFlags.no_shell_pages = 0;  // ++++++++++++++++++++ STATUS FLAG 
                            has_shell_pages = true;
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.pages_section_complete += ";\"" + pageTitle + "\"";

                        }
                    }


                    if (has_shell_pages)
                    {
                        aCourseBuildingStatusFlags.potential_errors += "Shell page(s) present in module(s)\n";// ++++++++++++++++++++ ERROR MESSAGE
                        aCourseBuildingStatusFlags.general_section_incomplete += ";\" Shell pages are present\"";
                    }
                    else
                    {
                        aCourseBuildingStatusFlags.general_section_complete += ";\" All shell pages have been removed\"";
                    }

                    //Console.WriteLine(jsonPages);
                    LogMessage("Received pages results: SUCCESS", LOG_LEVEL.DEBUG, true);
                    LogMessage("****************************************", LOG_LEVEL.DEBUG, true);
                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    LogMessage("\n\n *******************\n Exception - Course (" + courseCanvasId + ")." + e.ToString(), LOG_LEVEL.EXCEPTION, true, e);
                    Console.WriteLine("\n\n *******************\n Exception Course: {0} \n ************", courseCanvasId);
                }


                //***********************
                // E. Assignments
                //************************

                try
                {

                    var assignmentsData = await clsAssignmentsApi.listCourseAssignments(apiAccessToken, apiUrl, courseCanvasId);
                    //Console.WriteLine("assignmentsData dump:" + assignmentsData);
                    IEnumerable<JToken> assignmentIdTokens = assignmentsData.SelectTokens("$..id");
                    IEnumerable<JToken> assignmentNameTokens = assignmentsData.SelectTokens("$..name");
                    IEnumerable<JToken> assignmentDescriptionTokens = assignmentsData.SelectTokens("$..description");
                    IEnumerable<JToken> assignmentDueAtTokens = assignmentsData.SelectTokens("$..due_at");
                    IEnumerable<JToken> assignmentGroupCategoryIdTokens = assignmentsData.SelectTokens("$..group_category_id");

                    foreach (JToken assignmentId in assignmentIdTokens)
                    {
                        //listOfAssignmentIds.Add(assignmentId.ToString());
                        aCourseDetailsFromAPI.listOfAssignments.Add(new CourseDetailsFromAPI.Assignment((string)assignmentId));


                        //Console.WriteLine("Assignment Id Token: " + assignmentId.ToString());
                    }
                    aCourseDetailsFromAPI.numberOfAssignments = aCourseDetailsFromAPI.listOfAssignments.Count;


                    // >>>>>> [check# 11] ASSIGNMENTS_PRESENT
                    if (aCourseDetailsFromAPI.numberOfAssignments == 0)
                    {
                        //course is empty - set Db field assignements-present to false
                        //Console.WriteLine("Course (" + courseCanvasId + ") is empty!. ");
                        aCourseBuildingStatusFlags.assignments_present = 0; // ++++++++++++++++++++ STATUS FLAG
                        aCourseBuildingStatusFlags.potential_errors += "No assignments in the course!\n";// ++++++++++++++++++++ ERROR MESSAGE
                        aCourseBuildingStatusFlags.general_section_incomplete += ";\"Course has no assignments\"";
                    }
                    else aCourseBuildingStatusFlags.assignments_present = 1; // ++++++++++++++++++++ STATUS FLAG


                    // Abort - course is empty
                    // ***********************
                    //  General section - check # 2 if the course empty?
                    //***************************
                    if ((aCourseDetailsFromAPI.numberOfAssignments == 0) || (aCourseDetailsFromAPI.numberOfModules == 0))
                    {
                        //set all flags to negative // ++++++++++++++++++++ STATUS FLAG
                        aCourseBuildingStatusFlags.setAllValuesToFalse(); // ++++++++++++++++++++ STATUS FLAG
                        aCourseBuildingStatusFlags.skipped_checks = 0; // ++++++++++++++++++++ STATUS FLAG

                        aCourseBuildingStatusFlags.general_section_incomplete += ";\"Course is Empty\"";
                        //Console.WriteLine(" The course (" + courseCanvasId + " is empty! skipping other checks!");

                        writeCourseBuildingStatusToDB(aCourseBuildingStatusFlags); //write course status to DB
                        continue;
                    }


                    // Remove the discussion topic assignments 
                    //*** Discussion Topic Assignments are to be excluded from the checks
                    //    so remove them from the assignments list for this course
                    IEnumerable<JToken> assignmentDiscussionTopicIdTokens = assignmentsData.SelectTokens("$..discussion_topic.id");
                    foreach (JToken assignmentDiscussionTopicId in assignmentDiscussionTopicIdTokens)
                    {
                        aCourseDetailsFromAPI.RemoveAssignmentFromList(assignmentDiscussionTopicId.ToString());
                        //aCourseDetailsFromAPI.listOfAssignments.Remove((assignmentDiscussionTopicId.ToString());
                        Console.WriteLine("Removed assignment Discussion Topic Id Token: " + assignmentDiscussionTopicId.ToString());

                    }

                    //Console.WriteLine("List of Assignments " + aCourseDetailsFromAPI.listOfAssignments.ToString());


                    if (aCourseDetailsFromAPI.numberOfAssignments > 0) //course is not empty - proceed with testing
                    {

                        // ***********************
                        // E.1 Assignment Groups
                        // ***********************
                        dynamic assignmentGroupsData = await clsAssignmentsApi.listCourseAssignmentGroups(apiAccessToken, apiUrl, courseCanvasId);
                        IEnumerable<JToken> assignmentGroupIds = assignmentGroupsData.SelectTokens("$..id");
                        aCourseDetailsFromAPI.numberOfAssignmentGroups = assignmentGroupIds.Count();
                        IEnumerable<JToken> assignmentGroupNames = assignmentGroupsData.SelectTokens("$..name");
                        IEnumerable<JToken> assignmentGroupWeights = assignmentGroupsData.SelectTokens("$..group_weight");
                        //Console.WriteLine("Assignment Groups: " + assignmentGroupsData);

                        string assignmentGroupId, assignmentGroupTitle, assignmentGroupErrorMessage, assignmentGroupURLSuffix;
                        float totalGrades = 0;
                        bool shell_assignment_groups = false;
                        for (int i = 0; i < aCourseDetailsFromAPI.numberOfAssignmentGroups; i++)
                        {

                            assignmentGroupId = (string)assignmentGroupIds.ElementAt(i);
                            assignmentGroupTitle = (string)assignmentGroupNames.ElementAt(i);
                            assignmentGroupURLSuffix = "assignment_groups/" + assignmentGroupId;
                            aCourseDetailsFromAPI.listOfAssignmentGroups.Add(new CourseDetailsFromAPI.AssignmentGroup(assignmentGroupId));
                            // Name
                            aCourseDetailsFromAPI.listOfAssignmentGroups.ElementAt(i).name = assignmentGroupTitle;
                            //>>>>>> [check# 12] NO_SHELL_ASSIGNMENT_GROUPS
                            if (assignmentGroupTitle.Contains("[Deliverable Group]"))
                            {//shell course?
                                aCourseBuildingStatusFlags.no_shell_assignment_groups = 0; // ++++++++++++++++++++ STATUS FLAG
                                assignmentGroupErrorMessage = " Shell assignment group to be removedd";
                                aCourseBuildingStatusFlags.assignment_groups_section_incomplete += ";\"" + assignmentGroupTitle + "\"," +
                                                                                              "\"" + assignmentGroupErrorMessage + "\"," +
                                                                                               "\"" + assignmentGroupURLSuffix + "\"";
                                shell_assignment_groups = true;
                            }
                            else
                            {
                                aCourseBuildingStatusFlags.assignment_groups_section_complete += ";\"" + assignmentGroupTitle + "\"";
                            }


                            // Group weight
                            aCourseDetailsFromAPI.listOfAssignmentGroups.ElementAt(i).groupWeight = (float)assignmentGroupWeights.ElementAt(i);
                            totalGrades += aCourseDetailsFromAPI.listOfAssignmentGroups.ElementAt(i).groupWeight;
                        }
                        //Console.WriteLine("Assignement Groups grade total % " + totalGrade);
                        if (shell_assignment_groups)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Shell assignment groups present\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Course has shell assignment groups\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"Shell assignment groups have been removed\"";
                        }

                        // >>>>>> [check# 13] ASSIGNMENT_GROUPS_GRADES_TOTAL_100
                        if (totalGrades != 100)
                        {
                            aCourseBuildingStatusFlags.assignment_groups_grades_total_100 = 0; // ++++++++++++++++++++ STATUS FLAG
                            aCourseBuildingStatusFlags.potential_errors += "Assignment grade totals not 100%\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Grades total does not add up to 100% \"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"Grades total does add up to 100% \"";
                        }

                        LogMessage("Received assignmet groups results: SUCCESS", LOG_LEVEL.DEBUG, true);
                        LogMessage("****************************************", LOG_LEVEL.DEBUG, true);



                        // ***********************
                        // E.2 Individual Assignments
                        // ***********************
                        //Console.WriteLine("Assignement data JSON: \n" + assignmentsData.ToString());

                        bool unattachedAssignmentsPresent = false;
                        bool shellAssignmentsPresent = false;
                        bool assignmentsWithInvalidDueDatesPresent = false;
                        bool groupAssignmentsWithoutGroupDesignationPresent = true;
                        string assignmentId, assignmentTitle, assignmentErrorMessage, assignmentURLSuffix;

                        /* For reference
                        IEnumerable<JToken> assignmentIdTokens = assignmentsData.SelectTokens("$..id");
                        IEnumerable<JToken> assignmentNameTokens = assignmentsData.SelectTokens("$..name");
                        IEnumerable<JToken> assignmentDescriptionTokens = assignmentsData.SelectTokens("$..description");
                        IEnumerable<JToken> assignmentDueAtTokens = assignmentsData.SelectTokens("$..due_at");
                        IEnumerable<JToken> assignmentGroupCategoryIdTokens = assignmentsData.SelectTokens("$..group_category_id");
                        */
                        for (int i = 0; i < aCourseDetailsFromAPI.numberOfAssignments; i++)
                        {
                            //Console.WriteLine("Assignment Id " + assignmentIdTokens.ElementAt(i));
                            assignmentId = (string)assignmentIdTokens.ElementAt(i);
                            assignmentTitle = (string)assignmentNameTokens.ElementAt(i);

                            assignmentURLSuffix = "assignments/" + assignmentId;
                            Console.WriteLine(" Assignment id:" + assignmentId + " Title: " + assignmentTitle + " URL: " + assignmentURLSuffix);

                            // >>>>>> [check# 14] All Assignments Added to Modules
                            bool attachedToAModule = false;

                            //Console.WriteLine(" Checking if the  assignement " + listOfAssignments.ElementAt(i).id + " is attached to a module....");
                            foreach (CourseDetailsFromAPI.Module module in aCourseDetailsFromAPI.listOfModules) //is the assignement attached to a module?
                            {
                                //Console.WriteLine(" Module Content Ids -> " + module.listOfContentIds.ToString());
                                //Console.WriteLine(" Module Ids -> " + module.id);

                                //if ((module.listOfContentIds != null) && (!module.listOfContentIds.Any()))
                                if (module.listOfContentIds != null)
                                {
                                    foreach (string contentId in module.listOfContentIds)
                                    {
                                        //Console.WriteLine("Checking if Assignement " + listOfAssignments.ElementAt(i).id + " is attached to a Module ? -> " + module.id);
                                        if (aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).id.Equals(contentId))
                                        {
                                            //Console.WriteLine("Assignement " + aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).id + " IS ATTACHED to Module -> " + module.id);
                                            attachedToAModule = true;
                                        }
                                        if (attachedToAModule) break;
                                    }
                                }
                                else
                                {
                                    //Console.WriteLine( "Module: " + module.id + " has no content Ids.");
                                    //TBD: what do we do here?

                                }
                                if (attachedToAModule) break; //we can skip testing with more modules - we already found the one this assignment is attached to
                            }

                            if (!attachedToAModule)
                            {//sorry this assignement is a free agent
                                aCourseBuildingStatusFlags.all_assignments_added_to_modules = 0; // ++++++++++++++++++++ STATUS FLAG
                                assignmentErrorMessage = "Not attched to a module";
                                aCourseBuildingStatusFlags.assignments_section_incomplete += ";\"" + assignmentTitle + "\"," +
                                                                                               "\"" + assignmentErrorMessage + "\"," +
                                                                                                "\"" + assignmentURLSuffix + "\"";
                                unattachedAssignmentsPresent = true;
                            }



                            //>>>>>> [check# 15] NO_SHELL_ASSIGNMENTS
                            //if (!skipShellAssignmentsCheck)                            {

                            // add the name to the assingnment
                            //Console.WriteLine("Assignment name " + listOfAssignments.ElementAt(i).name);
                            bool shellAssignment = false;
                            if ((assignmentTitle != null) && (assignmentTitle.Contains("[Deliverable]")))
                            { //shell course?
                                aCourseBuildingStatusFlags.no_shell_assignments = 0; // ++++++++++++++++++++ STATUS FLAG
                                assignmentErrorMessage = "Shell assignment to be removed";
                                aCourseBuildingStatusFlags.assignments_section_incomplete += ";\"" + assignmentTitle + "\"," +
                                                                                             "\"" + assignmentErrorMessage + "\"," +
                                                                                             "\"" + assignmentURLSuffix + "\"";
                                shellAssignment = true; //error for this assignment          
                                shellAssignmentsPresent = true; //cumulative error
                            }

                            //}

                            //>>>>>> [check# 16] ASSIGNMENTS_VALID_DATES
                            //if (!skipAssignmentDueDatesCheck)                             {
                            //due_at
                            bool invalidDueDates = false;
                            aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).due_at = (string)assignmentDueAtTokens.ElementAt(i); // add the due at to the assingnment
                            if (aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).due_at != null)
                            {
                                //Console.WriteLine("Assignment due date " + listOfAssignments.ElementAt(i).due_at);
                                assignmentDueDate = DateTime.Parse(aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).due_at);
                                if ((assignmentDueDate < termStartDate) || (assignmentDueDate > termEndDate))
                                {
                                    aCourseBuildingStatusFlags.assignments_valid_dates = 0; // ++++++++++++++++++++ STATUS FLAG
                                    assignmentErrorMessage = "Due dates invalid/not specified";
                                    aCourseBuildingStatusFlags.assignments_section_incomplete += ";\"" + assignmentTitle + "\"," +
                                                                                                    "\"" + assignmentErrorMessage + "\"," +
                                                                                                    "\"" + assignmentURLSuffix + "\"";
                                    assignmentsWithInvalidDueDatesPresent = true;
                                    invalidDueDates = true;
                                }
                            }
                            //}
                            //>>>>>> [check# 17] ASSIGNMENTS_GROUP_DESIGNATION_DONE
                            //if (!skipAssignmentGroupDesignationCheck)                            {
                            //description 
                            aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).description = (string)assignmentDescriptionTokens.ElementAt(i);
                            // add the description to the assingnment
                            //Console.WriteLine("Assignment description " + listOfAssignments.ElementAt(i).description);
                            //group_category_id
                            aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).group_category_id = (string)assignmentGroupCategoryIdTokens.ElementAt(i); // add the group category id to the assingnment
                            bool groupDesignationNotDone = false;                                                                                                                                //Console.WriteLine("Assignment group category id " + listOfAssignments.ElementAt(i).group_category_id);
                            if (groupsAssignmentDescriptionLiterals.Any(s => aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).description.Contains(s)))
                            {
                                //Console.WriteLine("\n\n Group Assignment Name: " + aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).name);
                                //Console.WriteLine("Group Assignment Group Category ID: " + aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).group_category_id);
                                //Console.WriteLine("Group Assignment description: " + aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).description);

                                if (String.IsNullOrEmpty(aCourseDetailsFromAPI.listOfAssignments.ElementAt(i).group_category_id))
                                {
                                    ///Group assignement not properly assigned student groups
                                    aCourseBuildingStatusFlags.assignments_group_designation_done = 0; // ++++++++++++++++++++ STATUS FLAG
                                    assignmentErrorMessage = "Not designated as a group assignment";
                                    aCourseBuildingStatusFlags.assignments_section_incomplete += ";\"" + assignmentTitle + "\"," +
                                                                                                "\"" + assignmentErrorMessage + "\"," +
                                                                                                "\"" + assignmentURLSuffix + "\"";
                                    groupAssignmentsWithoutGroupDesignationPresent = true;
                                    groupDesignationNotDone = true;
                                }

                            }

                            if (!(!attachedToAModule || shellAssignment || invalidDueDates || groupDesignationNotDone))
                            {
                                // this assignment is alright
                                aCourseBuildingStatusFlags.assignments_section_complete += ";\"" + assignmentTitle + "\"";
                            }

                        }
                        if (!unattachedAssignmentsPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Assignment(s) not added to a module\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Assignment(s) unattached to modules present\"";

                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"All sssignments added to modules\"";
                        }

                        //if ((!skipShellAssignmentsCheck) && shellAssignments)
                        if (shellAssignmentsPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Shell assignment(s) present\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Shell Assignments to be removed\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_complete += ";\"Shell Assignments have been removed\"";
                        }

                        //if ((!skipAssignmentDueDatesCheck) && assignmentsInvalidDueDates)
                        if (assignmentsWithInvalidDueDatesPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Assignment(s) with invalid due date\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Assignments with invalid/unspecified due dates present\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"All sssignments have valid due dates\"";
                        }

                        //if ((!skipAssignmentGroupDesignationCheck) && !assignmentsGroupDesignationDone)
                        if (groupAssignmentsWithoutGroupDesignationPresent)
                        {
                            aCourseBuildingStatusFlags.potential_errors += "Group assignment(s) not properly designated?\n";// ++++++++++++++++++++ ERROR MESSAGE
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Group Assignments need to be properly designated\"";
                        }
                        else
                        {
                            aCourseBuildingStatusFlags.general_section_incomplete += ";\"Group Assignments have been properly designated\"";

                        }

                        LogMessage("Received assignmets results: SUCCESS", LOG_LEVEL.DEBUG, true);
                        LogMessage("****************************************", LOG_LEVEL.DEBUG, true);

                    }


                }
                catch (Exception e)
                {
                    //Console.WriteLine(e.ToString());
                    LogMessage("\n\n *******************\n Exception - Course (" + courseCanvasId + ")." + e.ToString() + "\n", LOG_LEVEL.EXCEPTION, true, e);
                    //Console.WriteLine("\n\n *******************\n Exception Course: {0} \n ************", courseCanvasId);
                }


                //Console.WriteLine(" CourseStatusCheckResults: " + aCourseBuildingStatusFlags.ToString());
                ////write course status to DB
                aCourseBuildingStatusFlags.skipped_checks = 0; // ++++++++++++++++++++ STATUS FLAG
                writeCourseBuildingStatusToDB(aCourseBuildingStatusFlags);

                if (testing) break;
            }//FOR LOOP list of courses

            working = false;
            return "complete";

        }


        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //                          HELPER METHODS
        // ********************************************************************


        /*
         * 
         */
        private bool writeCourseBuildingStatusToDB(CourseBuildingStatusFlags courseStatusData)
        {

            using (sqlServerDbConnection = new SqlConnection(sqlServerConnectionString))
            {
                sqlServerDbConnection.Open();

                SqlCommand command = sqlServerDbConnection.CreateCommand();
                SqlTransaction transaction;


                // Start a local transaction.
                transaction = sqlServerDbConnection.BeginTransaction("SampleTransaction");

                // Must assign both transaction object and connection
                // to Command object for a pending local transaction
                command.Connection = sqlServerDbConnection;
                command.Transaction = transaction;

                try
                {
                    /* Previously it was just one entry (the latest) per course everyday
                     * Changed to saving each of the results of hourly checks
                     * 
                     */
                    command.CommandText = "SELECT course_sis_id FROM canvas_course_building_status WHERE Status_date ='" + DateTime.Today.ToLocalTime().ToShortDateString() + "'" +
                                          " AND course_sis_id = '" + courseStatusData.course_sis_id + "'";
                    SqlDataReader reader = command.ExecuteReader();
                    bool courseStatusUpdateTodayExists = reader.HasRows;
                    reader.Close();


                    if (courseStatusUpdateTodayExists) //already status update from today exists
                    {

                        //Console.WriteLine(" Already a status update from today exists! Updating....");
                        //Update todays status for this course with new Time
                        String updateQuery = "UPDATE dbo.canvas_course_building_status SET " +
                                             " course_canvas_id = '" + aCourseBuildingStatusFlags.course_canvas_id +
                                             "', time_of_update = '" + aCourseBuildingStatusFlags.time_of_update +
                                             "', course_exists = '" + aCourseBuildingStatusFlags.course_exists +
                                             "', skipped_checks = '" + aCourseBuildingStatusFlags.skipped_checks +
                                             "', modules_present = '" + aCourseBuildingStatusFlags.modules_present +
                                             "', assignments_present = '" + aCourseBuildingStatusFlags.assignments_present +
                                             "', no_shell_modules = '" + aCourseBuildingStatusFlags.no_shell_modules +
                                             "', no_shell_pages = '" + aCourseBuildingStatusFlags.no_shell_pages +
                                             "', no_shell_assignments = '" + aCourseBuildingStatusFlags.no_shell_assignments +
                                             "', no_shell_assignment_groups = '" + aCourseBuildingStatusFlags.no_shell_assignment_groups +
                                             "', no_empty_modules = '" + aCourseBuildingStatusFlags.no_empty_modules +
                                             "', all_modules_published = '" + aCourseBuildingStatusFlags.all_modules_published +
                                             "', all_module_items_published = '" + aCourseBuildingStatusFlags.all_module_items_published +
                                             "', nonexam_modules_overview_present = '" + aCourseBuildingStatusFlags.nonexam_modules_overview_present +
                                             "', assignment_groups_grades_total_100 = '" + aCourseBuildingStatusFlags.assignment_groups_grades_total_100 +
                                             "', assignments_group_designation_done = '" + aCourseBuildingStatusFlags.assignments_group_designation_done +
                                             "', assignments_valid_dates = '" + aCourseBuildingStatusFlags.assignments_valid_dates +
                                             "', all_assignments_added_to_modules = '" + aCourseBuildingStatusFlags.all_assignments_added_to_modules +
                                             "', announcements_valid_dates = '" + aCourseBuildingStatusFlags.announcements_valid_dates +
                                             "', course_published = '" + aCourseBuildingStatusFlags.course_published +
                                             "', potential_errors = '" + aCourseBuildingStatusFlags.potential_errors +
                                             "', general_section_complete = '" + aCourseBuildingStatusFlags.general_section_complete +
                                             "', general_section_incomplete = '" + aCourseBuildingStatusFlags.general_section_incomplete +
                                             "', announcements_section_complete = '" + aCourseBuildingStatusFlags.announcements_section_complete +
                                             "', announcements_section_incomplete = '" + aCourseBuildingStatusFlags.announcements_section_incomplete +
                                             "', pages_section_complete = '" + aCourseBuildingStatusFlags.pages_section_complete +
                                             "', pages_section_incomplete = '" + aCourseBuildingStatusFlags.pages_section_incomplete +
                                             "', assignment_groups_section_complete = '" + aCourseBuildingStatusFlags.assignment_groups_section_complete +
                                             "', assignment_groups_section_incomplete = '" + aCourseBuildingStatusFlags.assignment_groups_section_incomplete +
                                             "', assignments_section_complete = '" + aCourseBuildingStatusFlags.assignments_section_complete +
                                             "', assignments_section_incomplete = '" + aCourseBuildingStatusFlags.assignments_section_incomplete +
                                             "', modules_section_complete = '" + aCourseBuildingStatusFlags.modules_section_complete +
                                             "', modules_section_incomplete = '" + aCourseBuildingStatusFlags.modules_section_incomplete +
                                             "' WHERE status_date = '" + DateTime.Today.ToLocalTime()
                                            + "' AND course_sis_id = '" + courseStatusData.course_sis_id + "'";
                        command.CommandText = updateQuery;
                        LogMessage("**************************************** UPDATE Query :" + updateQuery, LOG_LEVEL.DEBUG, true);
                        command.ExecuteNonQuery();

                    }
                    else  //first status update for today 
                    {

                        //Console.WriteLine(" First update of the day!");
                        //insert new row
                        String insertQuery = "INSERT INTO dbo.canvas_course_building_status (course_sis_id, course_canvas_id, status_date,time_of_update, course_exists, skipped_checks, modules_present, assignments_present," +
                                             "no_shell_modules, no_shell_pages, no_shell_assignments, no_shell_assignment_groups, no_empty_modules," +
                                             "all_modules_published, all_module_items_published, nonexam_modules_overview_present, assignment_groups_grades_total_100," +
                                             "assignments_group_designation_done, assignments_valid_dates, all_assignments_added_to_modules, announcements_valid_dates,  course_published, potential_errors, " +
                                             "general_section_complete, general_section_incomplete, announcements_section_complete, announcements_section_incomplete, " +
                                             "pages_section_complete, pages_section_incomplete, assignment_groups_section_complete, assignment_groups_section_incomplete, " +
                                             "assignments_section_complete, assignments_section_incomplete)" +
                                             " VALUES ('" +
                                                    aCourseBuildingStatusFlags.course_sis_id + "','" +
                                                    aCourseBuildingStatusFlags.course_canvas_id + "','" +
                                                    aCourseBuildingStatusFlags.status_date + "','" +
                                                    aCourseBuildingStatusFlags.time_of_update + "'," +
                                                    aCourseBuildingStatusFlags.course_exists + "," +
                                                    aCourseBuildingStatusFlags.skipped_checks + "," +
                                                    aCourseBuildingStatusFlags.modules_present + "," +
                                                    aCourseBuildingStatusFlags.assignments_present + "," +
                                                    aCourseBuildingStatusFlags.no_shell_modules + "," +
                                                    aCourseBuildingStatusFlags.no_shell_pages + "," +
                                                    aCourseBuildingStatusFlags.no_shell_assignments + "," +
                                                    aCourseBuildingStatusFlags.no_shell_assignment_groups + "," +
                                                    aCourseBuildingStatusFlags.no_empty_modules + "," +
                                                    aCourseBuildingStatusFlags.all_modules_published + "," +
                                                    aCourseBuildingStatusFlags.all_module_items_published + "," +
                                                    aCourseBuildingStatusFlags.nonexam_modules_overview_present + "," +
                                                    aCourseBuildingStatusFlags.assignment_groups_grades_total_100 + "," +
                                                    aCourseBuildingStatusFlags.assignments_group_designation_done + "," +
                                                    aCourseBuildingStatusFlags.assignments_valid_dates + "," +
                                                    aCourseBuildingStatusFlags.all_assignments_added_to_modules + "," +
                                                    aCourseBuildingStatusFlags.announcements_valid_dates + "," +
                                                    aCourseBuildingStatusFlags.course_published + ",'" +
                                                    aCourseBuildingStatusFlags.general_section_complete + "','" +
                                                    aCourseBuildingStatusFlags.general_section_incomplete + "','" +
                                                    aCourseBuildingStatusFlags.announcements_section_complete + "','" +
                                                    aCourseBuildingStatusFlags.announcements_section_incomplete + "','" +
                                                    aCourseBuildingStatusFlags.pages_section_complete + "','" +
                                                    aCourseBuildingStatusFlags.pages_section_incomplete + "','" +
                                                    aCourseBuildingStatusFlags.assignment_groups_section_complete + "','" +
                                                    aCourseBuildingStatusFlags.assignment_groups_section_incomplete + "','" +
                                                    aCourseBuildingStatusFlags.modules_section_complete + "','" +
                                                    aCourseBuildingStatusFlags.modules_section_incomplete + "','" +
                                                    //add space in case if potential error is a null string
                                                    aCourseBuildingStatusFlags.potential_errors + " '" +
                                                    ")";

                        command.CommandText = insertQuery;
                        LogMessage("**************************************** Insert Query :" + insertQuery, LOG_LEVEL.DEBUG, true);
                        command.ExecuteNonQuery();

                    }


                    // Attempt to commit the transaction.
                    transaction.Commit();
                    LogMessage("Course status written to database. \n**************", LOG_LEVEL.INFO, true);
                    sqlServerDbConnection.Close();
                    return true;
                }
                catch (Exception e)
                {
                    //Console.WriteLine("Commit Exception Type: {0}", e.GetType());
                    //Console.WriteLine("  Message: {0}", e.Message);
                    LogMessage(" Exception " + e.ToString(), LOG_LEVEL.EXCEPTION, true, e);

                    // Attempt to roll back the transaction.
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception e2)
                    {
                        // This catch block will handle any errors that may have occurred
                        // on the server that would cause the rollback to fail, such as
                        // a closed connection.
                        //Console.WriteLine("Rollback Exception Type: {0}", e2.GetType());
                        //Console.WriteLine("  Message: {0}", e2.Message);
                        LogMessage(" Exception " + e2.ToString(), LOG_LEVEL.EXCEPTION, true, e2);
                    }
                    sqlServerDbConnection.Close();
                    return false;
                }
            }
        }


        // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //            UTILITY METHODS - Generic helpder methods
        // ********************************************************************


        /* --------------------------------------------------------------------------------
         * getStringFromOracleReader()
         * 
         * Utility method to deal with Oracle DBNull 
         * In C# null means the absence of a reference to an object, 
         * whereas DBNull represents an uninitialized field or nonexistent database column.
         * ---------------------------------------------------------------------------------
         */
        private string getStringFromOracleReader(Object o)
        {
            if (o == DBNull.Value)
                return null;
            else
            {
                //LogMessage((string)o, LOG_LEVEL.DEBUG, true);
                return (string)o;
            }
        }

        /* --------------------------------------------------------------------------------
         * LogMessage(string msg, LOG_LEVEL level, bool console, Exception err = null)
         * 
         * Write a message to the log file, and if asked to the console window
         * Prevents us from having to write two print statements all over the place
         * ----------------------------------------------------------------------------------
         */
        private void LogMessage(string msg, LOG_LEVEL level, bool console, Exception err = null)
        {


            switch (level)
            {
                case LOG_LEVEL.DEBUG:
                    {
                        _logger.Debug(msg);
                    }
                    break;

                case LOG_LEVEL.INFO:
                    {
                        _logger.Info(msg);
                    }
                    break;

                case LOG_LEVEL.WARN:
                    {
                        _logger.Warn(msg);
                    }
                    break;

                case LOG_LEVEL.ERROR:
                    {
                        _logger.Error(msg);
                    }
                    break;

                case LOG_LEVEL.EXCEPTION:
                    {
                        _logger.Error(err, msg);
                    }
                    break;
                case LOG_LEVEL.FATAL:
                    {
                        _logger.Fatal(err, msg);
                    }
                    break;

                default:
                    {
                        _logger.Debug(msg);
                    }
                    break;
            }
            if (console)
                Console.WriteLine(msg);
        }

        /* -----------------------------------------------------------------
         * printFormattedJson(string jsonString)
         * 
         * Use JSON.net library to print in readabale format
         * ------------------------------------------------------------------
         */
        private void printFormattedJson(string jsonString) { }


    }//class - CourseBuildingStatusChecker

}
