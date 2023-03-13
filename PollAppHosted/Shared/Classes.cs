using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PollAppHosted.Shared
{
    public class Poll
    {
        public string Question { get; set; }
        public int ID { get; set; }
        public DateTime? EndDate { get; set; }
        public List<PollOption> Options { get; set; }

        public struct PollOption
        {
            private int _votes;
            
            public string Name;
            public int Votes { get { return _votes; } set { _votes = value; } }
            public int ID;
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
            return new SessionInfo { ID = s.ID, Name = (s.SessionName == null) ? s.ID.ToString() : s.SessionName, CreationDate = s.creationTime };
        }
    }

    public class Session
    {
        private Poll? _activePoll;

        public List<UserSessionRecord> users;
        public List<Tuple<string, int>>? activeResponses;
        public List<PollResult>? PollResults;
        public int ID;
        public string SessionName;
        public Poll? activePoll { get { return _activePoll; } set { _activePoll = value; } }
        public bool IsActive;
        public DateTime creationTime;

    }
    public struct PollResult
    {
        public Poll poll;
        public Dictionary<int, int> optionVotes;
    }
    public struct UserSessionRecord
    {
        private string _role;

        public string UserID;
        public string? UserName;
        public DateTime joinTime;
        public string Role { get { return _role; } set { _role = value; } }
    }
}
