using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;


public class dataSource : MonoBehaviour
{
    public enum serverConnectStage
    {
        NoServerSocket,
        ServerSocketCreated,
        ServerSocketBound,
        ServerSocketListening,
        ServerSocketAccepted,
        PacketReceived,
        PacketRead
    };

    public enum clientConnectStage
    {
        NoClientSocket,
        ClientSocketCreated,
        ClientSocketConnected,
        PacketSent
    };

    public bool mServer = false;//ARGUMENT
    public string mSourceIP = "10.0.0.242";// //"192.168.1.106";//ARGUMENT
    public int mPort = 9985;//ARGUMENT

    public int mCurrentTick;
    int mLastSendTick;//Last time we sent a packet.
    int mLastSendTimeMS;//Last time we sent a packet.
    int mTickInterval = 30;
    int mStartDelay = 5;//Still necessary?
    int mPacketCount;
    int mMaxPackets = 20;
    int mPacketSize = 1024;

    public serverConnectStage mServerStage;
    public clientConnectStage mClientStage;

    public bool mReadyForRequests;//flag to user class (eg terrainPager) that we can start adding requests.

    private Socket mListenSocket = null;
    private Socket mWorkSocket = null;

    IPAddress mSourceIPAddress;
    IPEndPoint mRemoteEndPoint;
    //IPHostEntry ipHostInfo;

    bool mListening = false;
    bool mAlternating = false;
    bool mConnectionEstablished = false;
    bool mFinished = false;

    byte[] mReturnBuffer;
    byte[] mSendBuffer;
    char[] mStringBuffer;

    public short mSendControls;
    public short mReturnByteCounter;
    public short mSendByteCounter;

    //For other classes, we define all the possible opcodes like this, for readability.
    public static int OPCODE_BASE = 1;

    public dataSource(bool server)
    {
        Debug.Log("Datasource constructor!!!");

        mServerStage = serverConnectStage.NoServerSocket;
        mClientStage = clientConnectStage.NoClientSocket;

        mServer = server;
        if (server)
        {
            mServer = true;
            mListening = true;
        }
    }

    // Start is called before the first frame update
    public void Start()
    {
        mReturnBuffer = new byte[1024];
        mSendBuffer = new byte[1024];
        mStringBuffer = new char[1024];

        //ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        //mSourceIPAddress = ipHostInfo.AddressList[0];
        mSourceIPAddress = IPAddress.Parse(mSourceIP);//IPAddress.Any;// 
        mRemoteEndPoint = new IPEndPoint(mSourceIPAddress, mPort);
        
        mSendByteCounter = sizeof(short);//Have to reserve two bytes at the beginning of mSendBuffer for the controls count.

        Debug.Log("Starting dataSource! sourceIPAddress " + mSourceIPAddress.ToString() + " remoteEndPoint " + mRemoteEndPoint.ToString());
        //mWorkSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //Note: ListenSocket unnecessary if !mServer and !mAlternating
        //mListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        if (mServer)
        {
            mListening = true;
            mServerStage = serverConnectStage.NoServerSocket;
        }
        else
        {
            mListening = false;
            mClientStage = clientConnectStage.NoClientSocket;
        }
    }
    
    private void OnDestroy()
    {
        disconnectSockets();
    }

    private void disconnectSockets()
    {
        if (mListenSocket != null)
        {
            mListenSocket.Close();
            mListenSocket = null;
        }
        if (mWorkSocket != null)
        {
            mWorkSocket.Close();
            mWorkSocket = null;
        }
    }

    public void tick()
    {
        if (mServer)
        {
            switch (mServerStage)
            {
                case serverConnectStage.NoServerSocket:
                    createListenSocket(); break;
                case serverConnectStage.ServerSocketCreated:
                    bindListenSocket(); break;
                case serverConnectStage.ServerSocketBound:
                    connectListenSocket(); break;
                case serverConnectStage.ServerSocketListening:
                    acceptConnection(); break;
                case serverConnectStage.ServerSocketAccepted:
                    receivePacket(); break;
                case serverConnectStage.PacketReceived:
                    readPacket(); break;
                case serverConnectStage.PacketRead:
                    mServerStage = serverConnectStage.ServerSocketListening;
                    break;
            }
        }
        else
        {
            Debug.Log("Client dataSource stage: " + mClientStage.ToString());
            switch (mClientStage)
            {
                case clientConnectStage.NoClientSocket:
                    connectSendSocket(); break;
                case clientConnectStage.ClientSocketCreated:
                    break;
                case clientConnectStage.ClientSocketConnected:
                    sendPacket();
                    break;
                case clientConnectStage.PacketSent:
                    mClientStage = clientConnectStage.ClientSocketConnected;
                    //mCurrentTick = 501;//FIX: cheap hack to get us out of main loop in main.cpp
                    break;
            }
        }
        mCurrentTick++;
    }

    /*
    void openListenSocket()
    {
        //if (mListenSocket.Connected == false)
        //{
        Debug.Log("LISTEN SOCKET attempting to bind...");
        mListenSocket.Bind(mRemoteEndPoint);
        mListenSocket.Listen(10);
        Debug.Log("Listen socket connected? " + mListenSocket.Connected.ToString());
        //}
    }*/

    public void createListenSocket()
    {
        Debug.Log("SERVER createListenSocket...");
        mListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //mListenSocket.Blocking = false;
        if (mListenSocket != null)
            mServerStage = serverConnectStage.ServerSocketCreated;
    }

    public void bindListenSocket()
    {
        Debug.Log("SERVER bindListenSocket...");
        try
        {
            mListenSocket.Connect(mRemoteEndPoint);
            mServerStage = serverConnectStage.ServerSocketBound;

        }
        catch (Exception e)
        {
            Debug.Log("bindListenSocket ERROR: " + e.ToString());
        }
    }

    public void connectListenSocket()
    {
        Debug.Log("SERVER connectListenSocket...");
        mListenSocket.Listen(100);
        mServerStage = serverConnectStage.ServerSocketListening;

    }

    public void acceptConnection()
    {
        mPacketCount = 0;
        Debug.Log("SERVER acceptConnection...");
        mListenSocket.BeginAccept(new AsyncCallback(AcceptCallback),
                mListenSocket);
        mServerStage = serverConnectStage.ServerSocketAccepted;
    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.
        //allDone.Set();

        // Get the socket that handles the client request.
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.
        //StateObject state = new StateObject();
        //mWorkSocket = handler;
        //handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
        //    new AsyncCallback(ReadCallback), state);
    }

    public void receivePacket()
    {
        Debug.Log("SERVER receivePacket...");
        if (mWorkSocket.Connected)
        {
            int bytes = mWorkSocket.Receive(mReturnBuffer, 1024, 0);
            if (bytes>0)
                mServerStage = serverConnectStage.PacketReceived;
        }

    }

    public void readPacket()
    {
        Debug.Log("SERVER readPacket...");
        short opcode, controlCount;//,packetCount;

        controlCount = readShort();
        for (short i = 0; i < controlCount; i++)
        {
            opcode = readShort();
            if (opcode == OPCODE_BASE)
            {   ////  keep contact, but no request /////////////////////////
                handleBaseRequest();
            }// else if (opcode==22) { // send us some number of packets after this one
             //	packetCount = readShort();
             //	if ((packetCount>0)&&(packetCount<=mMaxPackets))
             //		mPacketCount = packetCount;
             //}
        }

        clearReturnPacket();

        mServerStage = serverConnectStage.PacketRead;
    }

    public void clearReturnPacket()
    {
        Debug.Log("SERVER clearReturnPacket...");
        Array.Clear(mStringBuffer, 0, 1024);
        Array.Clear(mReturnBuffer, 0, 1024);        
    }

    public void allocateBuffers()
    {
        Debug.Log("SERVER allocateBuffers...");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void connectSendSocket()
    {
        Debug.Log("CLIENT connectSendSocket ");

        mWorkSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        if (mWorkSocket.Connected == false)
        {
            mWorkSocket.Connect(mRemoteEndPoint);
        }
        if (mWorkSocket.Connected)
            mClientStage = clientConnectStage.ClientSocketConnected;
    }

    public void sendPacket()
    {
        Debug.Log("CLIENT sendPacket ");
        byte[] bytes_received = new byte[1024];
        byte[] temp = new byte[sizeof(short)];
        int c = 0;
        int pos = 0;

        addBaseRequest();

        //Now, add the total controls count to the beginning of the data string... 
        temp = BitConverter.GetBytes((short)mSendControls);
        for (int i = 0; i < sizeof(short); i++) mSendBuffer[0 + i] = temp[i];

        Debug.Log("Sending " + mSendControls + " controls, " + mSendByteCounter + " bytes,  mCurrentTick " + mCurrentTick);
        mWorkSocket.Send(mSendBuffer);
        Debug.Log("Sent packet!");

        mSendByteCounter = sizeof(short);
        mSendControls = 0;
        clearSendPacket();

        mClientStage = clientConnectStage.PacketSent;
    }

    public void clearSendPacket()
    {
        Debug.Log("CLIENT clearSendPacket...");
        Array.Clear(mSendBuffer, 0, 1024);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void writeShort(short value)
    {
        byte[] bytes = new byte[sizeof(short)];
        bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < sizeof(short); i++) mSendBuffer[mSendByteCounter + i] = bytes[i];        
        mSendByteCounter += sizeof(short);
    }

    public void writeInt(int value)
    {
        byte[] bytes = new byte[sizeof(int)];
        bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < sizeof(int); i++) mSendBuffer[mSendByteCounter + i] = bytes[i];
        mSendByteCounter += sizeof(int);
    }

    public void writeFloat(float value)
    {
        byte[] bytes = new byte[sizeof(float)];
        bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < sizeof(float); i++) mSendBuffer[mSendByteCounter + i] = bytes[i];
        mSendByteCounter += sizeof(float);
    }

    public void writeDouble(double value)
    {
        byte[] bytes = new byte[sizeof(double)];
        bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < sizeof(double); i++) mSendBuffer[mSendByteCounter + i] = bytes[i];
        mSendByteCounter += sizeof(double);
    }

    public void writeString(string value)
    {
        short length = (short)value.Length;
        writeInt(length);
        System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
        byte[] bytes = encoding.GetBytes(value);
        for (short i = 0; i < length; i++) mSendBuffer[mSendByteCounter + i] = bytes[i];
        mSendByteCounter += length;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    
    public short readShort()
    {
        byte[] bytes = new byte[sizeof(short)];
        for (int i = 0; i < sizeof(short); i++) bytes[i] = mReturnBuffer[mReturnByteCounter + i];
        mReturnByteCounter += sizeof(short);
        short value = BitConverter.ToInt16(bytes,0);
        return value;
    }

    public int readInt()
    {
        byte[] bytes = new byte[sizeof(int)];
        for (int i = 0; i < sizeof(int); i++) bytes[i] = mReturnBuffer[mReturnByteCounter + i];
        mReturnByteCounter += sizeof(int);
        int value = BitConverter.ToInt32(mReturnBuffer, 0);
        return value;
    }

    public float readFloat()
    {
        byte[] bytes = new byte[sizeof(float)];
        for (int i = 0; i < sizeof(float); i++) bytes[i] = mReturnBuffer[mReturnByteCounter + i];
        mReturnByteCounter += sizeof(float);
        float value = BitConverter.ToSingle(mReturnBuffer, 0);
        return value;
    }

    public double readDouble()
    {
        byte[] bytes = new byte[sizeof(double)];
        for (int i = 0; i < sizeof(double); i++) bytes[i] = mReturnBuffer[mReturnByteCounter + i];
        mReturnByteCounter += sizeof(double);
        double value = BitConverter.ToDouble(mReturnBuffer, 0);
        return value;
    }

    public string readString()
    {
        int length = readInt();
        for (short i = 0; i < length; i++) mStringBuffer[i] = (char)mReturnBuffer[mReturnByteCounter + i];
        mReturnByteCounter += (short)length;
        string value = new string(mStringBuffer);
        return value;
    }
    
    public void clearString()
    {
        Array.Clear(mStringBuffer,0,mStringBuffer.Length);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void addBaseRequest()
    {
        short opcode = 1;//base request
        mSendControls++;//Increment this every time you add a control.
        writeShort(opcode);
        writeInt(mCurrentTick);
    }

    public void handleBaseRequest()
    {
        int tick = readInt();
        if (mServer) Debug.Log("dataSource clientTick = " + tick.ToString() + ", my tick " + mCurrentTick.ToString());
        else Debug.Log("dataSource serverTick = " + tick.ToString() + ", my tick " + mCurrentTick.ToString());
    }
}



//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


/*
   int bytes = mWorkSocket.Receive(bytes_received, 1024, 0);

   float numCodes = BitConverter.ToSingle(bytes_received, pos); pos += sizeof(float);
   for (int i = 0; i < numCodes; i++)
   {
       int OPCODE = (int)(BitConverter.ToSingle(bytes_received, pos)); pos += sizeof(float);
       if (OPCODE == 101)
       {
           float igFrame = BitConverter.ToSingle(bytes_received, pos); pos += sizeof(float);
           Debug.Log("returned code " + (int)OPCODE + ", frame " + (int)igFrame);
       }
       else
       {
           Debug.Log("Unknown IG OPCODE: " + OPCODE);
       }
   }*/


/*
if ((mCurrentTick++ % mTickInterval == 0) &&//(!mFinished) &&
    (mCurrentTick > mStartDelay))
{
    Debug.Log("dataSource ticking...");
    if (mConnectionEstablished == false)
    {
        trySockets();
    }
    else
    {
        if (mListening)
        {
            listenForPacket();
            if (mAlternating)
            {
                mListening = false;
                addBaseRequest();
                if (mServer) tick();
            }
        }
        else
        {
            sendPacket();
            if (mAlternating)
            {
                mListening = true;
                if (!mServer) tick();
            }
            else
                addBaseRequest();
        }
    }
}*/

/*
void trySockets()
{
    Debug.Log("Trysockets, ... ");
    if (mServer)
    {
        if (mConnectionEstablished == false)
        {
            openListenSocket();
            mConnectionEstablished = true;
        }
        else
        {
            Debug.Log("Trysockets, connecting... ");
            //connectListenSocket();
            Debug.Log("Trysockets, listening for packet!");
            listenForPacket();
        }
    }
    else
    {
        if (mWorkSocket.Connected == false)
        {
            connectSendSocket();
        }
        else
        {
            sendPacket();
            mConnectionEstablished = true;
        }
    }
}*/
