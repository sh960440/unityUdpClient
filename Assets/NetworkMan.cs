using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;

public class NetworkMan : MonoBehaviour
{
    public UdpClient udp; // socket
    public GameObject playerPrefab;

    private string myAddress = "";
    public Dictionary<string, GameObject> currentPlayers;
    public List<string> newPlayers;
    public List<string> droppedPlayers;

    

    // Start is called before the first frame update
    void Start()
    {
        // Initialize variables
        newPlayers = new List<string>();
        droppedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();

        // Connect to client
        udp = new UdpClient();
        Debug.Log("Connecting..."); 
		//udp.Connect("3.96.206.57", 12345);
        udp.Connect("localhost",12345);

        UpdateMessage message = new UpdateMessage();
        message.cmd = "connect";
        string m = JsonUtility.ToJson(message);
        Byte[] sendBytes = Encoding.ASCII.GetBytes(m);
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1);

        InvokeRepeating("UpdatePosition", 1, 1.0f/30.0f);


    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        PLAYER_CONNECTED,
        UPDATE,
        PLAYER_DISCONNECTED,
        CONFIRM_IP
    };
    
    [Serializable]
    public class MessageType{
        public commands cmd;
        public string id;
    }

    [Serializable]
    public class UpdateMessage
    {
        public string cmd;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class Player
    {
        [Serializable]
        public struct receivedPosition
        {
            public float X;
            public float Y;
            public float Z;
        }
        public string id;
        public receivedPosition position;
    }


    [Serializable]
    public class GameState{
        public Player[] players;
    }

    public MessageType latestMessage;
    public GameState lastestGameState;
    public Player spawningPlayer;
    public Player despawningPlayer;
    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);

        latestMessage = JsonUtility.FromJson<MessageType>(returnData);


        try{
            switch(latestMessage.cmd){
                case commands.PLAYER_CONNECTED:
                    Debug.Log("Player connected: " + latestMessage.id);
                    newPlayers.Add(latestMessage.id);
                    break;
                case commands.UPDATE:
                    lastestGameState = JsonUtility.FromJson<GameState>(returnData);
                    Debug.Log("Update; " + returnData);
                    break;
                case commands.PLAYER_DISCONNECTED:
                    Debug.Log("Player disconnected: " + latestMessage.id);
                    droppedPlayers.Add(latestMessage.id);
                    break;
                case commands.CONFIRM_IP:
                    Debug.Log("My address: " + latestMessage.id);
                    myAddress = latestMessage.id;
                    break;
                default:
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }


    void SpawnPlayers()
    {
        foreach (string s in newPlayers)
        {
            if (s==null)
            { 
            }
            else if (!currentPlayers.ContainsKey(s))
            {
                currentPlayers.Add(s, Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[s].GetComponent<NetworkID>().Id = s;
            }
        }
        newPlayers.Clear();
    }


    void UpdatePlayers(){
        for (int i = 0; i < lastestGameState.players.Length; i++)
        {
            if (currentPlayers.ContainsKey(lastestGameState.players[i].id))
            {
                if (currentPlayers[lastestGameState.players[i].id] != null)
                {
                    if (lastestGameState.players[i].id != myAddress)
                        currentPlayers[lastestGameState.players[i].id].GetComponent<Transform>().position = new Vector3(lastestGameState.players[i].position.X, lastestGameState.players[i].position.Y, lastestGameState.players[i].position.Z);
                }
                else
                {
                    currentPlayers[lastestGameState.players[i].id] = Instantiate(playerPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                }
            }
            else
            {
                newPlayers.Add(lastestGameState.players[i].id);
            }
        }
        
    }

    void DestroyPlayers()
    {
        foreach (string s in droppedPlayers)
        {
            if (s == null)
            {

            }
            else if (currentPlayers.ContainsKey(s))
            {
                Destroy(currentPlayers[s]);
                Debug.Log("Remove Player from List: " + currentPlayers.Remove(s));
            }
        }
        droppedPlayers.Clear();
    }

    void HeartBeat()
    {
        UpdateMessage message = new UpdateMessage();
        message.cmd = "heartbeat";
        string m = JsonUtility.ToJson(message);
        Byte[] sendBytes = Encoding.ASCII.GetBytes(m);
        udp.Send(sendBytes, sendBytes.Length);
    }

    void UpdatePosition()
    {
        if (currentPlayers.ContainsKey(myAddress))
        {
            UpdateMessage message = new UpdateMessage();
            message.cmd = "updatePosition";
            message.x = currentPlayers[myAddress].transform.position.x;
            message.y = currentPlayers[myAddress].transform.position.y;
            message.z = currentPlayers[myAddress].transform.position.z;
            string m = JsonUtility.ToJson(message);
            Byte[] sendBytes = Encoding.ASCII.GetBytes(m);
            udp.Send(sendBytes, sendBytes.Length);
        }
    }

    void Update(){
        
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
        
        if (Input.GetKey(KeyCode.W))
        {
            currentPlayers[myAddress].transform.Translate(new Vector3(0.0f, 0.0f, 0.1f));
        }
        if (Input.GetKey(KeyCode.S))
        {
            currentPlayers[myAddress].transform.Translate(new Vector3(0.0f, 0.0f, -0.1f));
        }
        if (Input.GetKey(KeyCode.A))
        {
            currentPlayers[myAddress].transform.Translate(new Vector3(-0.1f, 0.0f, 0.0f));
        }
        if (Input.GetKey(KeyCode.D))
        {
            currentPlayers[myAddress].transform.Translate(new Vector3(0.1f, 0.0f, 0.0f));
        }
    }

    void OnApplicationQuit()
    {
        OnDestroy();
    }
}
