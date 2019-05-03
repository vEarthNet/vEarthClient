using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class worldDataSource : dataSource
{

    static int OPCODE_INIT_TERRAIN = 21;
    static int OPCODE_TERRAIN = 22;
    static int OPCODE_INIT_SKYBOX = 23;
    static int OPCODE_SKYBOX = 24;

    //terrainPagerData mD;

    public bool mFullRebuild;

    public bool mTerrainDone;
    public bool mSkyboxDone;

    public int mSkyboxStage;

    public worldDataSource (bool server) : base ( server)
    {
        
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Starting worldDataSource!");

        base.Start();

    }

    // Update is called once per frame
    public void tick()
    {
        Debug.Log("Ticking worldDataSource!");

    }
    
    private void readPacket()
    {
        short opcode, controlCount;//,packetCount;

        controlCount = readShort();
        for (short i = 0; i < controlCount; i++)
        {
            opcode = readShort();
            if (opcode == OPCODE_BASE)
            {   ////  keep contact, but no request /////////////////////////
                handleBaseRequest();
            }
            else if (opcode == OPCODE_INIT_TERRAIN)
            { //initTerrainRequest finished
                Debug.Log("initTerrainRequest finished");
                //HERE: set a variable! 
            }
            else if (opcode == OPCODE_TERRAIN)
            {//terrainRequest finished
                Debug.Log("terrainRequest finished");
                mTerrainDone = true;
            }
            else if (opcode == OPCODE_INIT_SKYBOX)
            {//initSkyboxRequest finished
                Debug.Log("initSkyboxRequest finished");
            }
            else if (opcode == OPCODE_SKYBOX)
            {//skybox finished
                Debug.Log("skyboxRequest finished");
                mSkyboxDone = true;
            }

        }

        clearReturnPacket();

        mServerStage = serverConnectStage.PacketRead;
    }

    public void addInitTerrainRequest()//(terrainPagerData* data,const char* path)
    {
        Debug.Log("addInitTerrainRequest");
    }

    public void addTerrainRequest(float playerLong, float playerLat)
    {
        Debug.Log("addTerrainRequest " + playerLong.ToString() + " " + playerLat.ToString());
    }

    public void addInitSkyboxRequest()//unsigned int skyboxRes, int cacheMode,const char* path)
    {
        Debug.Log("addInitSkyboxRequest");
    }

    public void addSkyboxRequest(float tileLong, float tileLat, float playerLong, float playerLat, float playerAlt)
    {
        Debug.Log("addSkyboxRequest");
    }
}
