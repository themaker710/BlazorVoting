using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PollAppHosted.Shared
{
    public class Poll
    {
        private string _question;
        private int _id;
        private DateTime? _endDate;
        private List<PollOption> _options;


        public string Question { get { return _question; } set { _question = value; } }
        public int ID { get { return _id; } set { _id = value; } }
        public DateTime? EndDate { get { return _endDate; } set { _endDate = value; } }
        public List<PollOption> Options { get { return _options; } set { _options = value; } }

        public struct PollOption
        {
            private string _name;
            private int _votes;
            private int _id;
            
            public string Name { get { return _name; } set { _name = value; } }
            public int Votes { get { return _votes; } set { _votes = value; } }
            public int ID { get { return _id; } set { _id = value; } }
        }
    }

    
    public struct SessionInfo
    {
        public int ID;
        public string Name;
        public DateTime CreationDate;

        public bool IsEmpty => ID == 0;

        public static implicit operator SessionInfo(Session s)
        {
            return new SessionInfo { ID = s.ID, Name = s.Name ?? s.ID.ToString(), CreationDate = s.creationTime };
        }
    }

    public class Session
    {
        private List<UserSessionRecord> _users;
        private List<Tuple<string, int>>? _activeResponses;
        private List<PollResult>? _pollResults;
        private int _id;
        private string _name;
        private Poll? _activePoll;
        private SessionState _state;
        private DateTime _creationTime;

        public List<UserSessionRecord> users { get { return _users; } set { _users = value; } }
        public List<Tuple<string, int>>? activeResponses { get { return _activeResponses; } set { _activeResponses = value; } }
        public List<PollResult>? PollResults { get { return _pollResults; } set { _pollResults = value; } }
        public int ID { get { return _id; } set { _id = value; } }
        public string Name { get { return _name; } set { _name = value; } }
        public Poll? activePoll { get { return _activePoll; } set { _activePoll = value; } }
        public SessionState State { get { return _state; } set { _state = value; } }
        public DateTime creationTime { get { return _creationTime; } set { _creationTime = value; } }

        public bool IsEmpty => ID == 0;

    }
    public enum SessionState
    {
        Inactive,
        Idle,
        Polling,
        Ended
    }
    public enum UserStatus
    {
        Voter,
        Admin,
        Disconnected, 
        Spectator
    }

    public struct PollResult
    {
        public Poll poll;
        public Dictionary<int, int> optionVotes;
    }
    public struct UserSessionRecord
    {
        private UserStatus _role;
        private string _userid;
        private string? _username;
        private DateTime _dt;

        public string UserID { get { return _userid; } set { _userid = value; } }
        public string? UserName { get { return _username; } set { _username = value; } }
        public DateTime joinTime { get { return _dt; } set { _dt = value; } }
        public UserStatus Role { get { return _role; } set { _role = value; } }
    }
}
