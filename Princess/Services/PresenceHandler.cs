using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Princess.Data;
using Princess.Models;

namespace Princess.Services;

public class PresenceHandler
{
    private readonly PresenceDbContext _ctx;

    public PresenceHandler(PresenceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task RegisterToClass(ulong studentId, ulong classId)
    {
        var schoolClass = await GetClass(classId);
        var student = await _ctx.Students
            .Include(s => s.Classes)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        student.Classes.Add(schoolClass);

        await _ctx.SaveChangesAsync();
    }
    public async Task RegisterStudent(string name, ulong id, ulong classId)
    {
        var schoolClass = await GetClass(classId);

        var student = new Student()
        {
            Id = id,
            Name = name,
            Classes = new List<Class>()
            {
                schoolClass
            },
            Presences = new List<Presence>(),
            Lectures = new List<Lecture>()
        };

        await _ctx.Students.AddAsync(student);
        await _ctx.SaveChangesAsync();
    }

    // returns a lecture with the given id. Includes class, student, the student's precenses adn teh teacher.
    // Make sure to match the precens' id with the lecture.
    public async Task<Lecture> GetLectureAsync(int id)
    {
        var lecture = await _ctx.Lectures.Include(l => l.Students).ThenInclude(s => s.Presences)
            .Include(l => l.Class)
            .Include(l => l.Teacher)
            .Where(l => l.Id == id).FirstOrDefaultAsync();
        if (lecture != null) return lecture;

        return null;
    }

    // get presences connected to certain lecture
    public async Task<List<Presence>> GetPresencesAsync(int lectureId)
    {
        var presenceList = _ctx.Presences.Include(l => l.Lecture).Include(p => p.Student)
            .Where(l => l.Lecture.Id == lectureId).ToList();


        if (presenceList != null) return presenceList;

        return null;
    }
    

    public async Task<List<Presence>> GetAllAttendees(DateTime date, string selectedClass, string selectedTeacher)
    {
        return await _ctx.Presences
            .Include(x => x.Student)
            .ThenInclude(x => x.Lectures)!
            .ThenInclude(x => x.Class)
            .ThenInclude(x => x.Teachers)
            .Where(x => x.Lecture.Date == date && x.Lecture.Class.Name == selectedClass &&
                        x.Lecture.Teacher!.Name == selectedTeacher)
            .ToListAsync();
    }

    //Depends on ui using may change into just 1 method via changing Attended attribute!
    public async Task<List<Presence>> GetAllPresenceAttendees(DateTime date, string selectedClass,
        string selectedTeacher)
    {
        return await _ctx.Presences
            .Include(x => x.Student)
            .ThenInclude(x => x.Lectures)!
            .ThenInclude(x => x.Class)
            .ThenInclude(x => x.Teachers)
            .Where(x => x.Attended && x.Lecture.Date == date && x.Lecture.Class.Name == selectedClass &&
                        x.Lecture.Teacher!.Name == selectedTeacher)
            .ToListAsync();
    }

    public async Task<List<Presence>> GetAllAbsenceAttendees(DateTime date, string selectedClass,
        string selectedTeacher)
    {
        return await _ctx.Presences
            .Include(x => x.Student)
            .ThenInclude(x => x.Lectures)!
            .ThenInclude(x => x.Class)
            .ThenInclude(x => x.Teachers)
            .Where(x => !x.Attended && x.Lecture.Date == date && x.Lecture.Class.Name == selectedClass &&
                        x.Lecture.Teacher!.Name == selectedTeacher)
            .ToListAsync();
    }

    public async Task<List<Presence>> GetStudentsPresences(string studentName)
    {
        return await _ctx.Presences
            .Include(x => x.Lecture).Include(x => x.Lecture.Teacher)
            .ThenInclude(x => x.Classes)
            .Include(x => x.Student)
            .Where(x => x.Student.Name == studentName)
            .ToListAsync();
    }

    //gonna make through date listing
    public List<Presence> DateFilterOfPresences(List<Presence> query, DateTime startDate, DateTime endDate,
        string selectedClass, string selectedTeacher)
    {
        return query.Where(presence => (presence.Lecture.Date.Month > startDate.Month ||
                                        presence.Lecture.Date.Month == startDate.Month &&
                                        presence.Lecture.Date.Day >= startDate.Day)
                                       &&
                                       (presence.Lecture.Date.Month < endDate.Month ||
                                        presence.Lecture.Date.Month == endDate.Month &&
                                        presence.Lecture.Date.Day <= endDate.Day)).Where(x =>
            x.Lecture.Class.Name == selectedClass && x.Lecture.Teacher.Name == selectedTeacher).ToList();
    }

    public async Task<Lecture> RegisterAbsenceForStudent(ulong studentId, ulong classId, DateTime date, ulong? teacherId = null, string? reason = null)
    {
        var message = reason ?? "Absence reported";

        var student = await _ctx.Students
           .FirstOrDefaultAsync(x => x.Id == studentId);

        var classget =await  _ctx.Classes
            .FirstOrDefaultAsync(x => x.Id == classId);

        var lecture = await _ctx.Lectures
            .FirstOrDefaultAsync(x => x.Class == classget && x.Date == date);
        

        if (teacherId != null && lecture == null)
        {
            Teacher teacher = await _ctx.Teachers
                .FirstOrDefaultAsync(t => t.Id == teacherId);
            var newLecture = new Lecture()
            {
                Teacher = teacher,
                Date = date,
                Class = classget,
                Students = classget.Students,
                Presences = new List<Presence>()
            };

            await _ctx.Lectures.AddAsync(newLecture);
            await _ctx.SaveChangesAsync();

            lecture = await _ctx.Lectures
                .FirstOrDefaultAsync(x => x.Class == classget && x.Date == date); ;
        }

        if (teacherId == null && lecture == null)
        {
            var newLecture = new Lecture()
            {
                Date = date,
                Class = classget,
                Students = classget.Students,
                Presences = new List<Presence>()
            };

            await _ctx.Lectures.AddAsync(newLecture);
            await _ctx.SaveChangesAsync();

            lecture = await _ctx.Lectures
                .FirstOrDefaultAsync(x => x.Class == classget && x.Date == date); ;
        }

        var presence = new Presence
        {
            Attended = false,
            ReasonAbsence = message,
            Student = student,
            Lecture = lecture
        };

        _ctx.Presences.Add(presence);

        await _ctx.SaveChangesAsync();
        return lecture;
    }

    public async Task<List<Class>> GetAllSchoolclasses()
    {
        var schoolClasses = await _ctx.Classes
            .Include(c => c.Teachers)
            .Include(c => c.Students)
            .Include(c => c.Lectures)
            .ThenInclude(c => c.Presences)
            .ToListAsync();

        return schoolClasses;
    }

    public async Task<List<Teacher>> GetAllTeachers()
    {
        var teachers = await _ctx.Teachers
            .Include(c => c.Classes)
            .Include(c => c.Lectures)
            .ThenInclude(l => l.Presences)
            .ToListAsync();

        return teachers;
    }

    public async Task<Class> GetClass(ulong classId)
    {
        return await _ctx.Classes
            .Where(x => x.Id == classId)
            .Include(s => s.Students)
            .FirstOrDefaultAsync();
    }

    //Adds the member from discord to table "Teachers" in database
    public async Task RegisterTeacherToDatabase(DiscordMember member, Class classToAdd)
    {
        var newTeacher = new Teacher
        {
            Id = member.Id,
            Name = member.Nickname ?? member.Username
        };

        _ctx.Teachers.Add(newTeacher);
        await _ctx.SaveChangesAsync();

        if (newTeacher.Classes != null) newTeacher.Classes.Add(classToAdd);
        if (newTeacher.Classes == null)
            newTeacher.Classes = new List<Class>();
        newTeacher.Classes.Add(classToAdd);

        await _ctx.SaveChangesAsync();
    }

    //If the user exists in the table "Teachers" database, return true
    public async Task<bool> TeacherExists(ulong newTeacherId, Class classToAdd)
    {
        var teacher = await _ctx.Teachers
            .Where(t => t.Id == newTeacherId)
            .Include(cl => cl.Classes)
            .FirstOrDefaultAsync();

        if (teacher != null) return teacher.Classes.Any(c => c.Id == classToAdd.Id);

        return false;
    }

    public async Task<bool> StudentExists(ulong newStudentId)
    {
        var student = await _ctx.Students
            .Where(s => s.Id == newStudentId)
            
            .FirstOrDefaultAsync();

        if (student != null) return true;

        return false;
    }
    //If the user exists in the table "Student" database, return true
    public async Task<bool> StudentExists(ulong newStudentId, Class classToAdd)
    {
        var student = await _ctx.Students
            .Where(s => s.Id == newStudentId)
            .Include(cl => cl.Classes)
            .FirstOrDefaultAsync();

        if (student != null) return student.Classes.Any(c => c.Id == classToAdd.Id);

        return false;
    }

    public async Task RemoveStudentFromClassInDb(ulong newStudentId, Class classToRemoveFrom)
    {
        var student = await _ctx.Students
            .Where(n => n.Id == newStudentId)
            .FirstAsync();

        var classObj = await _ctx.Classes
            .Where(x => x.Equals(classToRemoveFrom))
            .Include(s => s.Students)
            .FirstAsync();

        classObj.Students.Remove(student);

        await _ctx.SaveChangesAsync();
    }

    public async Task<Lecture> RegisterPresence(ulong studentId, ulong classId, DateTime date, ulong teacherId, string? reason = null)
    {
        var student = await _ctx.Students
            .Include(s => s.Lectures)
            .Include(s => s.Presences)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        var classget = await _ctx.Classes
            .Where(x => x.Id == classId)
            .FirstOrDefaultAsync();

        var lecture = await _ctx.Lectures
            .Where(x => x.Class == classget && x.Date == date)
            .FirstOrDefaultAsync();

        var teacher = await _ctx.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);

        if (lecture == null)
        {
            var newLecture = new Lecture()
            {
                Date = date,
                Class = classget,
                Students = classget.Students,
                Presences = new List<Presence>(),
                Teacher = teacher
                
            };

            await _ctx.Lectures.AddAsync(newLecture);

            await _ctx.SaveChangesAsync();

            lecture = await _ctx.Lectures
                .Where(x => x.Class == classget && x.Date == date)
                .FirstOrDefaultAsync(); 

        }
        else
        {
            lecture.Teacher = teacher;
            await _ctx.SaveChangesAsync();
        }
        var presence = new Presence
        {
            Attended = true,
            ReasonAbsence = reason,
            Student = student,
            Lecture = lecture
        };

        await _ctx.Presences.AddAsync(presence);

        await _ctx.SaveChangesAsync();

        return lecture;
    }
}