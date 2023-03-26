using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using MudBlazor.Extensions;
using PollAppHosted.Shared;
using System.ComponentModel.Design;

namespace PollAppHosted.Server.Hubs
{
    public class VoteHub : Hub
    {

        static List<Session> sessions = new List<Session>();
        static Dictionary<int, int> sessionsDict = new Dictionary<int, int>();

        //Create a new session, setting the caller as the admin.
        public async Task CreateSession(string sessionName, string? username = null)
        {
            //Get user ID
            string userID = Context.ConnectionId;

            //Create a unique session ID
            int sessionID = 0;
            bool uniqueID = false;
            while (!uniqueID)
            {
                sessionID = new Random().Next(1000, 9999);
                if (!sessionsDict.ContainsKey(sessionID))
                    uniqueID = true;
            }

            //Create new session
            Session newSession = new() { ID = sessionID, Name = sessionName, State = SessionState.Inactive, creationTime = DateTime.Now, users = new List<UserSessionRecord>(), PollResults = new List<PollResult>()};
            newSession.users.Add(new UserSessionRecord { UserID = userID, UserName = (string.IsNullOrWhiteSpace(username) ? "Creator" : username), joinTime = DateTime.Now, Role = UserStatus.Admin });
            sessions.Add(newSession);
            UpdateSessions();
            //Add user to session group
            await Groups.AddToGroupAsync(Context.ConnectionId, newSession.ID.ToString()).WaitAsync(new TimeSpan(-1));
            //Send caller session ID
            await Clients.Caller.SendAsync("ReceiveSession", newSession, $"Session succesfully created with code {newSession.ID}");
        }
        //Leave a session
        public async Task LeaveSession(int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];

            //Check if user is registered for session
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID))
            {
                //Remove user from session
                sessions[sessionIndex].users.RemoveAll(x => x.UserID == userID);
                //Remove user from session group
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionID.ToString()).WaitAsync(new TimeSpan(-1));
                //Send updated session to all admins for the session
                await Clients.Users(sessions[sessionIndex].users.Where(x => x.Role == UserStatus.Admin).Select(x => x.UserID.ToString()).ToArray()).SendAsync("ReceiveSession", sessions[sessionIndex], "Session Updated");
            }
        }
        //Send connection ID to caller
        public async Task SendConnectionID() => await Clients.Caller.SendAsync("ReceiveID", Context.ConnectionId);

        public async Task SendMessage(string msg) => await Clients.All.SendAsync("ReceiveMessage", msg);

        public async Task SendSessionList() => await Clients.Caller.SendAsync("ReceiveSessions", sessions);

        public async Task PullSession(int sessionID) => await Clients.Caller.SendAsync("ReceiveSession", sessions[sessionsDict[sessionID]], "Session Updated");

        public async Task RefreshSession(int sessionID)
        {
              
            foreach (UserSessionRecord record in sessions[sessionID].users) await Clients.User(record.UserID).SendAsync("RecieveSession", sessions[sessionID]);
        }

        //Join a session
        public async Task JoinSession(int sessionID, string? username)
        {

            //Check if session exists
            if (!sessionsDict.ContainsKey(sessionID))
            {
                //Send response to caller indicating session does not exist
                await Clients.Caller.SendAsync("ReceiveSession", new Session(), "Session does not exist. Check your sesssion code");
                return;
            }

            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];

            //Check if user is already registered for session
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID))
            {
                //Check if user is admin, and thus redirect to session dashboard page
                if (sessions[sessionIndex].users.Find(x => x.UserID == userID).Role == UserStatus.Admin)
                {
                    await Clients.Caller.SendAsync("ReceiveSessionAdmin", sessions[sessionIndex], "Reconnecting to Session Manager");
                    return;
                }
            }
            else
            {
                //Add user to session and group
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionID.ToString());

                string uname;
                if (string.IsNullOrWhiteSpace(username))
                    uname = "User " + sessions[sessionIndex].users.Count;
                else
                    uname = username;

                sessions[sessionIndex].users.Add(new UserSessionRecord { UserID = userID, UserName = uname, joinTime = DateTime.Now, Role = UserStatus.Voter });
            }

            //Send updated session to all admins for the session
            await Clients.Users(sessions[sessionIndex].users.Where(x => x.Role == UserStatus.Admin).Select(x => x.UserID.ToString()).ToArray()).SendAsync("ReceiveSession", sessions[sessionIndex], "Refreshed Session Data");

            //Send response to caller
            await Clients.Caller.SendAsync("ReceiveSession", sessions[sessionIndex], "Session connection successful");
            Console.WriteLine("got here");
        }
        
        public void UpdateSessions()
        {
            //Sync sessionsDict with sessions list
            sessionsDict = new Dictionary<int, int>();

            for (int i = 0; i < sessions.Count; i++)
                sessionsDict.Add(sessions[i].ID, i);
        }
        public PollResult CountCurrentVotes(int sessionID)
        {
            int sessionIndex = sessionsDict[sessionID];

            PollResult result = new PollResult() { poll = sessions[sessionIndex].activePoll, optionVotes = new Dictionary<int, int>() };

            int[] count = new int[sessions[sessionIndex].activePoll.Options.Count];


            foreach (var response in sessions[sessionIndex].activeResponses)
                count[response.Item2]++;

            int total = 0;
            //find total count in int[]
            foreach (int c in count)
                total += c;

            result.totalVotes = total;

            //Set count array to PollResult structure
            for (int i = 0; i < count.Length; i++)
                result.optionVotes.Add(i, count[i]);

            return result;
        }

        public async Task SendPoll(Poll poll, int sessionID)
        {
            int id = sessionsDict[sessionID];
            sessions[id].activePoll = poll;
            sessions[id].activeResponses = new List<Tuple<string, int>>();
            sessions[id].State = SessionState.Polling;
            await Clients.Groups(sessionID.ToString()).SendAsync("ReceivePoll", poll);
            //Are groups working?
        }
        public async Task UpdateState(SessionState state, int sessionID)
        {
            int id = sessionsDict[sessionID];
            sessions[id].State = state;
            await Clients.Groups(sessionID.ToString()).SendAsync("ReceiveSession", sessions[id].Name, "State Updated");

        }

        public async Task UpdateUsers(List<UserSessionRecord> users, int sessionID)
        {
            int id = sessionsDict[sessionID];

            //Combine the local list and the users paramater, giving duplicate priority to the parameter
            foreach (UserSessionRecord user in users)
            {
                if (sessions[id].users.Exists(x => x.UserID == user.UserID))
                {
                    sessions[id].users.RemoveAll(x => x.UserID == user.UserID);
                }
                sessions[id].users.Add(user);
            }

            await Clients.Groups(sessionID.ToString()).SendAsync("ReceiveSession", sessions[id]);
        }

        public async Task EndPoll(int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];
            //Check if user is admin
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == UserStatus.Admin))
            {
                //Count all active responses and add to session votes
                PollResult pr = CountCurrentVotes(sessionID);

                sessions[sessionIndex].PollResults.Add(pr);

                sessions[sessionIndex].activePoll = null;
                sessions[sessionIndex].State = SessionState.Idle;

                //Send poll results to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceivePollResults", pr, sessions[sessionIndex]);
                //Set active poll to null

            }
            else
                await Clients.Caller.SendAsync("ReceiveMessage", "You do not have the neccessary permissions to do that!");
        }
    
        public async Task ActivateSession(int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];
            //Check if user is admin
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == UserStatus.Admin))
            {
                //Activate session
                sessions[sessionIndex].State = SessionState.Idle;
                //Send response to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceiveMessage", "Session is now active.");
                if (sessions[sessionIndex].activePoll != null)
                    await Clients.Group(sessionID.ToString()).SendAsync("ReceivePoll", sessions[sessionIndex].activePoll);
            }
            else
                await Clients.Caller.SendAsync("ReceiveMessage", "You do not have the neccessary permissions to do that!");
        }

        //Forcibly close session
        public async Task EndSession(int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];
            //Check if user is admin
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == UserStatus.Admin))
            {
                //End session
                sessions[sessionIndex].State = SessionState.Ended;
                //Send response to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceiveMessage", "Session has been closed by an admin. Thank you for participating!");
                //Force client to send session leave request
                await Clients.Group(sessionID.ToString()).SendAsync("EndSession");
                
            }
            else
                await Clients.Caller.SendAsync("ReceiveMessage", "You do not have the neccessary permissions to do that!");
        }
        


        public async Task SendVote(int vote, int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];
            //Check if user is registered for session
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID))
            {
                //Check if session is polling
                if (sessions[sessionIndex].State == SessionState.Polling)
                {
                    //Check if user has already voted the same response (can change)
                    if (!sessions[sessionIndex].activeResponses.Exists(x => x.Item1 == userID && x.Item2 == vote))
                    {
                        //Add user to active responses
                        sessions[sessionIndex].activeResponses.Add(new Tuple<string, int>(userID, vote));
                        //Send response to caller
                        await Clients.Caller.SendAsync("ReceiveMessage", $"Your vote has been registered as '{sessions[sessionIndex].activePoll.Options[vote]}'.");
                        //Send count of responses to session admins
                        await Clients.Users(sessions[sessionIndex].users.Where(x => x.Role == UserStatus.Admin).Select(x => x.UserID.ToString()).ToArray()).SendAsync("ReceiveVotes", sessions[sessionIndex].activeResponses.Count());
                    }
                    else
                        await Clients.Caller.SendAsync("ReceiveMessage", "You have already selected this reponse.");
                }
                else
                    await Clients.Caller.SendAsync("ReceiveMessage", "The session is not active.");
            }
            else
                await Clients.Caller.SendAsync("ReceiveMessage", "You are not registered for this session.");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            //Remove user from all groups
            foreach (var session in sessions)
            {
                if (session.users.Exists(x => x.UserID == Context.ConnectionId))
                {
                    session.users.RemoveAll(x => x.UserID == Context.ConnectionId);
                    Groups.RemoveFromGroupAsync(Context.ConnectionId, session.ID.ToString()).Wait(new TimeSpan(-1));
                }
            }

            return base.OnDisconnectedAsync(exception);
        }
    }
}
