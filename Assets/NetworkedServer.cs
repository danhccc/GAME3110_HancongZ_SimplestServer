using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    const int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    const int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    // Start is called before the first frame update
    void Start()
    {
        // This must be done before all other operations
        NetworkTransport.Init();    
        
        // Info about timing and channels, such as time out.
        ConnectionConfig config = new ConnectionConfig();
        
        // Setting up channels (adding channels to ConnectionConfig)
        reliableChannelID = config.AddChannel(QosType.Reliable);    
        unreliableChannelID = config.AddChannel(QosType.Unreliable);

        // Host the config we created
        HostTopology topology = new HostTopology(config, maxConnections);


        hostID = NetworkTransport.AddHost(topology, socketPort, null);


        playerAccounts = new LinkedList<PlayerAccount>();
        // We need to laod our saved player accounts
        
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;          // ID of the host you're receiving the message from
        int recConnectionID;    // Represent the person sending to us
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        // NetworkTransport.Receive:: use this function to pull data and modify them; 'out' gets stuffs by references; 'bufferSize' can't be modify.
        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:              // This person still connecting
                break;
            case NetworkEventType.ConnectEvent:         // When a client connect
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:            // If sent any data
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);    // Turning byte into a string
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:      // When a client disconnect
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    } 
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg); // Turning string into a byte
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if(signifier == ClientToServerSignifiers.CreateAccount)
        {
            string n = csv[1];
            string p = csv[2];

            bool isUnique = false;

            foreach(PlayerAccount pa in playerAccounts)
            {
                if(pa.name == n)
                {
                    isUnique = true;
                    break;
                }
            }   
            
            if (!isUnique)
            {
                playerAccounts.AddLast(new PlayerAccount(n, p));

                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.Success, id);

                // When a new account added, save player account list!
            }
            else
            {
                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameInUse, id);
            }
        }

        else if (signifier == ClientToServerSignifiers.Login)
        {
            string n = csv[1];
            string p = csv[2];

            bool hasBeenFound = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {

                    if (pa.password == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.Success, id);
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureIncorrectPassword, id);
                    }

                    // We found the player account, do something

                    hasBeenFound = true;
                    break;
                }
            }

            if (!hasBeenFound)
            {
                SendMessageToClient(ServerToClientSignifiers.LoginResponse + "," + LoginResponses.FailureNameNotFound, id);
            }
        }
    }

    public class PlayerAccount
    {
        public string name, password;

        public PlayerAccount(string Name, string Password)
        {
            name = Name;
            password = Password;
        }
    }

    public static class ClientToServerSignifiers
    {
        public const int Login = 1;
        public const int CreateAccount = 2;
    }
    public static class ServerToClientSignifiers
    {
        public const int LoginResponse = 1;
    }

    public static class LoginResponses
    {
        public const int Success = 1;
        public const int FailureNameInUse = 2;
        public const int FailureNameNotFound = 3;
        public const int FailureIncorrectPassword = 4;
    }
}
