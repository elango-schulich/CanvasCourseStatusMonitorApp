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
    class CourseMetadata
    {
       
        public string course_sis_id { get; set; }
        public string course_title { get; set; }
        
        public string program_initials { get; set; }
        public string instructor_name { get; set; }
        public string instructor_email { get; set; }
        public string instructor_ppy { get; set; }
        public string admin_name { get; set; }
        public string admin_email { get; set; }
        public string admin_ppy { get; set; }

        //syllabus checks
        public int syllabus_notes_section_present { get; internal set; } = 1;
        public int syllabus_office_hours_section_present { get; internal set; } = 1;
        public int syllabus_learning_outcomes_section_present { get; internal set; } = 1;
        public int syllabus_course_materials_section_present { get; internal set; } = 1;
        public int syllabus_written_assignements_section_present { get; internal set; } = 1;
        public int syllabus_grade_calculation_section_present { get; internal set; } = 1;
        public int syllabus_preparation_section_present { get; internal set; } = 1;
        public int syllabus_class_participation_section_present { get; internal set; } = 1;
        public string syllabus_missing_sections { get; internal set; }

        //public int syllabus_check_passed { get; internal set; } = 1;
        //public string syllabus_check_exceptions { get; internal set; }

        public void CleanUpSingleQuotes()
        {
            if ((!String.IsNullOrEmpty(course_title)) && course_title.Contains("'"))
            {
                this.course_title = course_title.Replace("'", "''");
            }

            if ((!String.IsNullOrEmpty(admin_name)) && admin_name.Contains("'")){
                //Console.WriteLine(" Admin name with apostrophe: " + this.admin_name);
                this.admin_name = admin_name.Replace("'", "''");
                //Console.WriteLine(" Admin name with apostrophe removedd?: " + this.admin_name);
            }

            if ((!String.IsNullOrEmpty(instructor_name)) && instructor_name.Contains("'"))
            {
                this.instructor_name = instructor_name.Replace("'", "''");
            }
        }

        public CourseMetadata(string sisId)
        {
            this.course_sis_id = sisId;
            
        }
    }
}