using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourseStatusMonitorApp
{
    /*
     * Philosophy - Assume everything is ok until you evidence to the contrary
     * Default values for flags are always positive until we see some evidence to set it to negative
     */
	class CourseBuildingStatusFlags
	{

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


            //Syllabus - moved to course metadata
            //18. SYL_NOTES_SECTION_PRESENT
            //19. SYL_OFFICE_HOURS_SECTION_PRESENT
            //20. SYL_LEARNING_OUTCOMES_SECTION_PRESENT
            //21. SYL_COURSE_MATERIALS_SECTION_PRESENT
            //22. SYL_WRITTEN_ASSIGNEMENTS_SECTION_PRESENT
            //23. SYL_GRADE_CALCULATION_SECTION_PRESENT
            //24. SYL_PREPARATION_SECTION_PRESENT
            //25. SYL_CLASS_PARTICIPATION_SECTION_PRESENT

            //SYLLABUS_CHECK_PASSED
            //SYLLABUS_CHECK_EXCEPTIONS

            STATUS_CHECK_COMPLETE 

        */

        //key fields
        public string status_date { get; set; } = DateTime.Today.ToLocalTime().ToShortDateString();
        public string course_sis_id { get; set; }

        //Additional meta data 
        public string time_of_update { get; set; } = DateTime.Now.ToString("HH:mm"); //update time
        public string course_canvas_id { get; set; }  // canvas id 
        public int course_exists { get; set; } = 1;  //assume it exists in canavas unless proven it doesn't
        public int skipped_checks { get; set; } = 0; // did status check had to abort due to some unhandleable error?

        // *********** Basic checks ***************
        // empty course?
        public int modules_present { get; internal set; }
        public int assignments_present { get; internal set; }
        //shell course?
        public int no_shell_modules { get; set; } = 1;
        public int no_shell_pages { get; set; } = 1;
        public int no_shell_assignments { get; set; } = 1;
        public int no_shell_assignment_groups { get; set; } = 1;
        //basic check passed?
        public int basic_checks_passed { get; set; } = 0;

        //Module checks
        public int no_empty_modules { get; internal set; } = 1;
        public int all_modules_published { get; internal set; } = 1;
        public int all_module_items_published { get; internal set; } = 1;
        public int nonexam_modules_overview_present { get; internal set; } = 1;

        //assignment checks
        public int assignment_groups_grades_total_100 { get; internal set; } = 1;
        public int assignments_group_designation_done { get; internal set; } = 1;
        public int assignments_valid_dates { get; internal set; } = 1;
        public int all_assignments_added_to_modules { get; internal set; } = 1;
        //announcements
        public int announcements_valid_dates { get; internal set; } = 1;
        //course published
        public int course_published { get; internal set; } = 1;
        public string potential_errors { get; internal set; }
        /*
        //syllabus checks - moved to course meta data
        public int syllabus_notes_section_present { get; internal set; } = 1;
        public int syllabus_office_hours_section_present { get; internal set; } = 1;
        public int syllabus_learning_outcomes_section_present { get; internal set; } = 1;
        public int syllabus_course_materials_section_present { get; internal set; } = 1;
        public int syllabus_written_assignements_section_present { get; internal set; } = 1;
        public int syllabus_grade_calculation_section_present { get; internal set; } = 1;
        public int syllabus_preparation_section_present { get; internal set; } = 1;
        public int syllabus_class_participation_section_present { get; internal set; } = 1;
        public int syllabus_check_passed { get; internal set; } = 1;
        //public string syllabus_check_exceptions { get; internal set; }
        */

        //detailed error messages
        public string general_section_complete { get; internal set; }
        public string general_section_incomplete { get; internal set; }
        public string pages_section_complete { get; internal set; }
        public string pages_section_incomplete { get; internal set; }
        public string announcements_section_complete { get; internal set; }
        public string announcements_section_incomplete { get; internal set; }
        public string modules_section_complete { get; internal set; }
        public string modules_section_incomplete { get; internal set; }
        public string module_items_section_complete { get; internal set; }
        public string module_items_section_incomplete { get; internal set; }
        public string assignments_section_complete { get; internal set; }
        public string assignments_section_incomplete { get; internal set; }
        public string assignment_groups_section_complete { get; internal set; }
        public string assignment_groups_section_incomplete { get; internal set; }

        public CourseBuildingStatusFlags(string canvasId, string sisId)
        {
            this.course_canvas_id = canvasId;
            this.course_sis_id = sisId;
        }

        /*
         *  If course is Empty or it doesn't exists this method is called to set all values to false
         */
        public void setAllValuesToFalse() {

            this.no_shell_modules = 0;
            this.no_shell_pages = 0;
            this.no_shell_assignments = 0;
            this.no_shell_assignment_groups = 0;
            this.no_empty_modules = 0;
            this.all_modules_published = 0;
            this.all_module_items_published = 0;
            this.nonexam_modules_overview_present = 0;
            this.assignment_groups_grades_total_100 = 0;
            this.assignments_group_designation_done = 0;
            this.assignments_valid_dates = 0;
            this.all_assignments_added_to_modules = 0;
            this.announcements_valid_dates = 0;
            this.course_published = 0;
            /*
            this.syllabus_notes_section_present = 0;
            this.syllabus_office_hours_section_present = 0;
            this.syllabus_learning_outcomes_section_present = 0;
            this.syllabus_course_materials_section_present = 0;
            this.syllabus_written_assignements_section_present = 0;
            this.syllabus_grade_calculation_section_present = 0;
            this.syllabus_preparation_section_present = 0;
            this.syllabus_class_participation_section_present = 0;
            this.syllabus_check_passed = 0;
            */
        }
        
	}
}
