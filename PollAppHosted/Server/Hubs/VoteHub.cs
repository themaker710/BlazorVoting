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
        
        public async Task SendMessage(string msg)
        {
            await Clients.All.SendAsync("ReceiveMessage", msg);
        }

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
            newSession.users.Add(new UserSessionRecord { UserID = userID, UserName = username, joinTime = DateTime.Now, Role = "admin" });
            sessions.Add(newSession);
            UpdateSessions();
            //Add user to session group
            await Groups.AddToGroupAsync(Context.ConnectionId, newSession.ID.ToString());
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
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionID.ToString());
                //Send updated session to all admins for the session
                await Clients.Users(sessions[sessionIndex].users.Where(x => x.Role == "admin").Select(x => x.UserID.ToString()).ToArray()).SendAsync("ReceiveSession", sessions[sessionIndex], "Session Updated");
            }
        }
        public async Task SendSessionList()
        {
            await Clients.Caller.SendAsync("ReceiveSessions", sessions);
        }

        //Join a session
        public async Task JoinSession(int sessionID, string? username, string role)
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
                //Update user role
                UserSessionRecord record = sessions[sessionIndex].users.Find(x => x.UserID == userID);
                record.Role = role;
                sessions[sessionIndex].users.RemoveAll(x => x.UserID == userID);
                sessions[sessionIndex].users.Add(record);
            }
            else
            {
                ////Check if session is active
                //if (sessions[sessionIndex].State == SessionState.Inactive)
                //{
                //    //Send response to caller
                //    await Clients.Caller.SendAsync("ReceiveMessage", $"You have been successfully registered for the inactive {sessions[sessionIndex].Name} session. You will be notified when the session becomes active.");
                //}
                //Add user to session and group
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionID.ToString());
                sessions[sessionIndex].users.Add(new UserSessionRecord { UserID = userID, UserName = username, joinTime = DateTime.Now, Role = role });
                //Send caller active poll, if there is one
                if (sessions[sessionIndex].activePoll != null)
                    await Clients.Caller.SendAsync("ReceivePoll", sessions[sessionIndex].activePoll);
            }

            //Send updated session to all admins for the session
            await Clients.Users(sessions[sessionIndex].users.Where(x => x.Role == "admin").Select(x => x.UserID.ToString()).ToArray()).SendAsync("ReceiveSession", sessions[sessionIndex]);
            //Send response to caller
            await Clients.Caller.SendAsync("ReceiveSession", sessions[sessionIndex], "Session connection successful");
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

            //Set count array to PollResult structure
            for (int i = 0; i < count.Length; i++)
                result.optionVotes.Add(i, count[i]);

            return result;
        }

        public async Task SendPoll(Poll poll, int sessionID)
        {
            sessions[sessionsDict[sessionID]].activePoll = poll;
            await Clients.All.SendAsync("ReceivePoll", poll);

        }

        public async Task EndPoll(int sessionID)
        {
            //Get user ID
            string userID = Context.ConnectionId;
            //Get session index
            int sessionIndex = sessionsDict[sessionID];
            //Check if user is admin
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == "admin"))
            {
                //Send response to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceiveMessage", "Poll has been ended.");

                //Count all active responses and add to session votes
                sessions[sessionIndex].PollResults.Add(CountCurrentVotes(sessionID));

                //Send poll results to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceivePollResults", sessions[sessionIndex].activePoll);
                //Set active poll to null
                sessions[sessionIndex].activePoll = null;
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
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == "admin"))
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
            if (sessions[sessionIndex].users.Exists(x => x.UserID == userID && x.Role == "admin"))
            {
                //End session
                sessions[sessionIndex].State = SessionState.Ended;
                //Send response to all users for the session
                await Clients.Group(sessionID.ToString()).SendAsync("ReceiveMessage", "Session has been closed by an admin. Thank you for participating!");
                //Force client to send session leave request
                await Clients.Group(sessionID.ToString()).SendAsync("EndSession", sessionID);
                
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
                        //Send updated poll results to all users for the session
                        await Clients.Group(sessionID.ToString()).SendAsync("ReceivePollResults", sessions[sessionIndex].activeResponses);
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
    }
}
