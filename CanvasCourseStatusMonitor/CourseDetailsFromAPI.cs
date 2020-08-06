using NLog;
using System;
using System.Collections.Generic;

namespace CourseStatusMonitorApp
{
    /*
     * Design Philosophy - Assume everything is ok until you evidence to the contrary
     * Default values for flags are always positive until we see some evidence to set it to negative
     */
    class CourseDetailsFromAPI
	{

        private static Logger _logger = LogManager.GetLogger(typeof(CourseStatusMonitorApp).FullName);
        //*************************
        //key fields
        //*************************
        public string course_canvas_id { get; set; }  // canvas id 
        public string course_sis_id { get; set; }

        //*************************
        //Additional meta data 
        //*************************
        public string time_of_update { get; set; } = DateTime.Now.ToString("HH:mm"); //update time
        public int course_exists { get; set; } = 1;  //assume it exists in canavas unless proven it doesn't
        public int status_check_complete { get; set; } = 1; // did status check had to abort due to some unhandleable error?
        public List<Module> listOfModules = new List<Module>();
        public List<string> listOfAssignmentIds = new List<string>();
        public List<Assignment> listOfAssignments = new List<Assignment>();
        public List<AssignmentGroup> listOfAssignmentGroups = new List<AssignmentGroup>();
        public List<string> groupsAssignmentDescriptionLiterals = new List<string>(new string[] { "Group", "group", "team", "Team" });
        public List<Page> listOfPages = new List<Page>();
        public List<Announcement> listOfAnnouncements = new List<Announcement>();
        public int numberOfAssignments, numberOfModules, numberOfModuleItems, numberOfContentIds, numberOfAssignmentGroups, numberOfPages = 0;

        private enum LOG_LEVEL
        {
            DEBUG = 0,
            INFO,
            WARN,
            ERROR,
            EXCEPTION,
            FATAL
        }
        public  class ModuleItem
        {
            public string id { get; set; }
            public string title { get; set; }
            public string content_id { get; set; }
            public bool published { get; set; }

            public ModuleItem(string moduleItemId)
            {
                id = moduleItemId;
            }
        }

        public class Module
        {
            public string id { get; set; }
            public string name { get; set; }
            public bool published { get; set; }
            public List<ModuleItem> listOfModuleItems { get; set; }
            public List<string> listOfContentIds { get; set; }
            public Module(string moduleId)
            {
                id = moduleId;
                List<ModuleItem> listOfModuleItems = new List<ModuleItem>();
                List<string> listOfContentIds = new List<string>();
            }
        }

        public class Assignment
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string due_at { get; set; }
            public string group_category_id { get; set; }
            public Assignment(string assignmentId)
            {
                id = assignmentId;
            }
         }


        public class AssignmentGroup
        {

            public string id { get; set; }
            public string name { get; set; }
            public float groupWeight { get; set; }
            public AssignmentGroup(string assignmentGroupId)
            {
                id = assignmentGroupId;
            }
            //public List<String> memberAssignmentIds { get; set; }

        }

        public class Page
        {
            public string id { get; set; }
            public string title { get; set; }

            public Page(string pageId)
            {
                id = pageId;
            }
        }

        public class Announcement
        {
            public string id { get; set; }
            public string title { get; set; }
            public string posted_at { get; set; }
            public Announcement(string announcementId)
            {
                id = announcementId;
            }
        }
                   
        /* 
         * Constructor()
         */
        public CourseDetailsFromAPI(string canvasId, string sisId)
        {
            this.course_canvas_id = canvasId;
            this.course_sis_id = sisId;
            //courseStatusDataToDB = new CourseStatusCheckResults(canvasId, sisId);
        }


        /*
         * 
         * /
            
        public void setCourseExists(int trueOrFalse)
        {
            courseStatusDataToDB.course_exists = trueOrFalse;
        }

        //Used when course is reset and the course ID changes
        public void setCourseCanvasId(string newCourseId)
        {
            this.course_canvas_id = newCourseId;
            courseStatusDataToDB.course_canvas_id = newCourseId;
        }
        */

        /* 
         * Remove an assignment from the list 
         * Used for removing discussion topic assignments
         * Note: tricky to remove an object from the list you're iterating through
         */
        public void RemoveAssignmentFromList(string assignmentID)
        {
            foreach (Assignment assignment in listOfAssignments.ToArray())
            {
                if ((assignment.id).Equals(assignmentID))
                {
                    listOfAssignments.Remove(assignment);
                    break;
                }
            }
                
        }


        /*
        * check if a shell page is attached to any module  
        */
        public bool IsShellPageAttachedToAModule(string pageID)
        {
            foreach (Module module in listOfModules)  //get list of modules
            {
                foreach (ModuleItem moduleItem in module.listOfModuleItems) //get list of module items for each module
                {
                    LogMessage("Checking page if it's an orphan shell page (page ID: " + pageID + " ! \n**************", LOG_LEVEL.INFO, true);
                    if (moduleItem.id.Equals(pageID.Trim())) return true; //see if any of it matches the ID
                }
            }

            return false;
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

    }
}
