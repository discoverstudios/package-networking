using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


public class TCPClient : MonoBehaviour
{
    //-----------------------------------------------
    // Public Fields
    //-----------------------------------------------
    
    public int port;

    //-----------------------------------------------
    // Private Fields
    //-----------------------------------------------

    private string messageFromServer;
    private List<string> pendingMessages; //	messages that caller tried to send whilst disconnected
    private TcpClient socketConnection;
    private Thread clientReceiveThread;
    private string currentMessage;


    //-----------------------------------------------
    // Events
    //-----------------------------------------------

    public delegate void OnServerMessageReceived(string message);

    public event OnServerMessageReceived onServerMessageReceived;
    public event OnServerMessageReceived onServerConnectionFailed;

    //-----------------------------------------------
    // Mono Methods
    //-----------------------------------------------
    
    private void Update()
    {
        if (messageFromServer != currentMessage)
        {
            currentMessage = messageFromServer;
            onServerMessageReceived?.Invoke(currentMessage);
        }
    }

    //-----------------------------------------------
    // TCP Methods
    //-----------------------------------------------

    /// <summary> 	
    /// Setup socket connection. 	
    /// </summary> 	
    public void ConnectToTcpServer(string ipAddress)
    {
        try
        {
            //Debug.Log("Connect Attempt");
            socketConnection = new TcpClient(ipAddress, port);
            
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.Log("On client connect exception " + e);
        }
    }

    /// <summary> 	
    /// Runs in background clientReceiveThread; Listens for incomming data. 	
    /// </summary>     
    private void ListenForData()
    {
        try
        {
            //Debug.Log("Connecting to " + ip + ":" + port + "...");
            
            byte[] bytes = new byte[1024];
            
            while (socketConnection.Connected)
            {
                //	send out things we queued up whilst disconnected
                if (pendingMessages != null)
                {
                    //	Copy pending messages so if there's a problem we don't get stuck in a loop
                    var messages = pendingMessages;
                    pendingMessages = null;
                    foreach (var message in messages)
                        SendServerMessage(message);
                }

                // Get a stream object for reading 				
                using (NetworkStream stream = socketConnection.GetStream())
                {
                    int length;
                    // Read incomming stream into byte arrary. 					
                    while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        var incommingData = new byte[length];
                        Array.Copy(bytes, 0, incommingData, 0, length);
                        // Convert byte array to string message. 						
                        string serverMessage = Encoding.ASCII.GetString(incommingData);
                        messageFromServer = serverMessage;
                        Debug.Log($"<b>[S]</b> {serverMessage}");
                    }
                }
            }
        }
        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
            onServerConnectionFailed?.Invoke(socketException.Message);
            messageFromServer = socketException.ToString();
        }
    }
    
    /// <summary> 	
    /// Send message to server using socket connection. 	
    /// </summary> 	
    public void SendServerMessage(string messageToSend)
    {
        if (socketConnection == null || !socketConnection.Connected)
        {
            //	instead of silently losing messages, queue them up to be sent
            //throw new System.Exception("Not connected, message (" + messageToSend + " lost");
            if (pendingMessages == null)
                pendingMessages = new List<string>();
            pendingMessages.Add(messageToSend);
            Debug.Log("Tried to send message whilst not connected, queued. " + messageToSend);
            return;
        }

        try
        {
            // Get a stream object for writing. 			
            NetworkStream stream = socketConnection.GetStream();
            if (stream.CanWrite)
            {
                string clientMessage = messageToSend;
                // Convert string message to byte array.                 
                byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(clientMessage);
                // Write byte array to socketConnection stream.                 
                stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                Debug.Log($"<b>[C]</b> {clientMessage}");
            }
        }
        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
            messageFromServer = socketException.ToString();
        }
    }
}