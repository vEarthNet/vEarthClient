using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using System.Linq;
using Mono.Data.Sqlite;

using UnityEngine;
using UnityEditor;

//using EasyRoads3Dv3;

public struct TerrainPagerData
{    
    public string mSkyboxPath;
    public string mTerrainPath;
    public string mResourcePath;
    public string mSkyboxLockfile;//Keep in WorldDataSource? Or not?
    public string mTerrainLockfile;//Irrelevant.
    public string mTerrainHeightsBinFile;//One tile's worth of heights
    public string mTerrainTexturesBinFile;// ... and textures
    public string mTerrainTreesBinFile;// ... and trees.
    public string[] skybox_files;//Filenames for the five skybox textures, because we 
                                 //are going to have to flip and rotate them before they can be used in Torque.
                                 //FIX: make this six, and include the bottom one in FG. 
                                //(Although - really? Can it _ever_ be seen if all is working? Are we just wasting FG's time?)

    public float mMapCenterLongitude;
    public float mMapCenterLatitude;

    public float mMetersPerDegreeLongitude;
    public float mDegreesPerMeterLongitude;
    public float mMetersPerDegreeLatitude;
    public float mDegreesPerMeterLatitude;

    public float mClientPosLongitude;
    public float mClientPosLatitude;
    public float mClientPosAltitude;

    public float mTileLoadRadius;
    public float mTileDropRadius;

    public float mTileWidth;
    public float mTileWidthLongitude;
    public float mTileWidthLatitude;
    public float mSquareSize;

    public int mGridSize;
    public int mHeightmapRes;
    public int mTextureRes;
    public int mLightmapRes;
    public int mSkyboxRes;
    public int mSkyboxCacheMode;
}

//Maybe obsolete?
struct loadTerrainData
{
    public float startLongitude;
    public float startLatitude;
    public float tileDistance;
};

struct osmNode
{
    public int oseId;
    public char[] osmId;
    public float longitude;
    public float latitude;
};

struct osmWay
{
    public string type;
    public string name;
    public char[] osmId;
    public List<osmNode> nodes;

    //MeshRoad *road;
    //DecalRoad* road;
};

// HMMM, Not at all sure which way would be best here. Need to add coordinates to my terrain object. Making a separate list for now.
//Doubles is kind of a pain, but keeping them here because we need that resolution for shapes smaller than terrain tiles.
public class Coordinates
{
    public double longitude = 0;
    public double latitude = 0;

    public Coordinates()
    {
    }

    public Coordinates(double x, double y)
    {
        longitude = x;
        latitude = y;
    }
};

public struct mapNode
{
    public double latitude;
    public double longitude;
    public int feature_id;
    public int feature_node_id;
    public long node_id;//osm id
    public int connection_index;//0-3, which connection node to attach to, except WHOOPS, this is used by more than one feature node.
};

public struct mapFeature
{
    public int id;
    public string name;
    public string type;
    public string subtype;
    public string subsubtype;
};

public struct mapFeatureNode
{
    public int id;
}

public struct sharedFeatureNode
{
    public long node_id;
    public double latitude;
    public double longitude;
    public int feature_1;
    public int feature_2;
    public int feature_3;
    public int feature_4;
    public string f1_type;
    public string f2_type;
    public string f3_type;
    public string f4_type;
    public string f1_subtype;
    public string f2_subtype;
    public string f3_subtype;
    public string f4_subtype;
    public string f1_subsubtype;
    public string f2_subsubtype;
    public string f3_subsubtype;
    public string f4_subsubtype;

};

public struct mapFeatureConnection
{
    public string prefabName;
    public long node_id;
    public Vector3 pos;
    public float rot; //only dealing with vertical (Y) rotation at this stage.
    public long node_0;
    public long node_1;
    public long node_2;
    public long node_3;
};

public struct erRoadType
{
    public int id;
    public float roadWidth;
    public string roadMaterial;
    public int layer;
    public string tag;
    public bool sidewalks;
    public bool streetlights;
    public bool guardrails;
};

public struct erRoad
{
    public int id;
    public int feature_id;
    public int type_id;
    public string name;
}

public struct erRoadMarker
{
    public int id;
    public int road_id;
    public int control_type;
    public Vector3 pos;
    public Vector3 rot;
}

// The TerrainPager organizes sets of stored terrain tiles and skyboxes, and optionally can 
// maintain a WorldDataSource connection to an external process (FlightGear) for realtime updates.
public class TerrainPager : MonoBehaviour
{
    public enum PagerLoadStage
    {
        NothingLoaded,
        DataSourceConnected,
        DataSourceReady,
        DataRequested,
        TerrainDataAvailable,
        SkyboxDataAvailable,
        PagerRunning
    };

    public GameObject BaseTerrainObject;//This is the starter terrain, added to TerrainPager in the editor, from which it makes all the copies.
    public GameObject mPlayer;
    public GameObject mCamera;
    public GameObject MapNodePrefab;
    private Terrain BaseTerrain;

    //worldDataSource mDataSource;
    //dataSource mDataSource;
    worldDataSource mDataSource;
    public TerrainPagerData mD;//This is a standalone data struct for easy portability.//OR NOT?? What is easier about this actually?


    //public ERRoadNetwork mRoadNetwork;//TEMP, ROADS
    //public ERRoad mRoad;//TEMP, ROADS
    List<erRoadType> mRoadTypes;

    IDbConnection mDbConn;
    IDbCommand mDbCmd;
    public string mDbPath = "Resources/w130n40.db";

    List<GameObject> mTerrains; //This is the short list of actually loaded terrains.
    List<GameObject> mTerrainGrid; //This is the sparse array in the shape of a grid centered on player. (?)
    List<loadTerrainData> mRequestTiles; //Ever changing list of tiles that need to be requested.
    List<Coordinates> mTerrainCoords;//TEMP? Would like to have these added to the actual object, not a separate list.
    GameObject mTerrain; //This is the terrain the player or camera is currently occupying.
    List<string> mTerrainMaterials;
    //List<int> mLoadedFeatures;
    //List<int> mRoadedTerrains;

    List<short> mAllLandCovers;
    List<short> mUnfoundLandCovers;//temp

    List<int> mRoadIds;
    Dictionary<int,List<int>> mRoadMarkerIds;
    List<int> mConnectionIds;

    Dictionary<string, float> mCellGrid;

    Dictionary<long, osmWay> mRoads;
    Dictionary<long, osmWay> mActiveRoads;
    Dictionary<int, int> mStaticShapes;//int oseId
    Dictionary<int, UnityEngine.Object> mPrefabs;

    float[,,] tSplatmap;//Temporary splatMap used for storing tree inhibitory values near roads, etc.

    public bool mUseDataSource;
    bool mSentInitRequests;
    bool mSentTerrainRequest;
    bool mSentSkyboxRequest;
    bool mLoadedTileGrid;
    bool mForestStarted;
    bool mDoForest;
    bool mDoForestUpdates;
    bool mDoStreets;
    bool mDoStreetUpdates;
    bool mDoStaticShapes;
    bool mDoStaticShapeUpdates;

    Vector3 mClientPos;

    float mTileStartLongitude;//These refer to the bottom left corner of the tile
    float mTileStartLatitude;  //the client is currently standing on or over.

    Vector3 mStartPos;

    int mCurrentTick;
    int mTickInterval;

    int mLastSkyboxTick;
    int mSkyboxTickInterval;
    int mSkyboxLoadDelay;

    int mTerrainRequestTick;
    int mSkyboxRequestTick;

    int mLastForestTick;
    int mForestTickInterval;
    int mCellGridSize;

    PagerLoadStage mLoadStage;
    int mNumHeightBinArgs = 5;

    /////////////////////////////////////////////////////////////////////
    // ??? Still necessary in Unity ???
    //These are the only overlapping items between terrainPager and terrainPagerData - they are here because  
    //they need to be exposed to script for the terrainPager creation block in the mission.
    float mMapCenterLongitude;
    float mMapCenterLatitude;
    float mTileLoadRadius;//At a future time these could be weighted by axes, to get an eliptical area
    float mTileDropRadius;//instead of a circle, but definitely not necessary for first pass.
    float mForestRadius;
    float mStreetRadius;
    float mShapeRadius;
    float mForestTries;
    float mSkyboxRes;

    float mCellWidth;
    float mCellArea;
    float mMinCellArea;//Amount of free area below which we don't bother trying to add more trees. Default=10%.   
    ///////////////////////

    int mGridSize;
    int mGridMidpoint;

    float mLastTileStartLong;//For determining when we've crossed a tile border and 
    float mLastTileStartLat;  //need to reset the grid.

    bool mFreeCamera;

    bool mEditorMode = false;

    //ERRoadType roadTypeHwy, roadTypeMotorway, roadTypeDirt, roadTypeSidewalk, roadTypePath;//TEMP, ROADS
    //ERConnection X_SourceConn, T_SourceConn;//TEMP, ROADS

    //TEMP, just using this to have one place to go, while I figure out how to actually handle it.
    bool importRoads = false;
    bool convertRoads = false;
    bool makeRoads = false;

    // Start is called before the first frame update
    void Start()
    {
        mTickInterval = 1;
        mDbConn = null;
        mDbCmd = null;

        mPrefabs = new Dictionary<int, UnityEngine.Object>();
        
        if (mDbPath.Length > 0)
        {
            Debug.Log("opening database! " + mDbPath);
            OpenDatabase();
        }

        //string osmFile = Application.dataPath + "/OSM/map.32nd_to_36th.osm";// "/OSM/map.LCC.osm";//
        //ImportOpenStreetMap(osmFile);

        //NOTE: ERRoadNetwork is a singleton, so when you do this you get a reference to the one active road network in the scene already, not a new one.
        //mRoadNetwork = new ERRoadNetwork();//TEMP, ROADS

        mAllLandCovers = new List<short>();
        mUnfoundLandCovers = new List<short>();

        mRoadIds = new List<int>();
        mRoadMarkerIds = new Dictionary<int, List<int>>();
        mConnectionIds = new List<int>();

        mD.mTileLoadRadius = 1000.0f;//PUT IN EDITOR
        mD.mTileDropRadius = 10000.0f;//NOT USED
        mForestRadius = 320.0f;
        mStreetRadius = 320.0f;
        mShapeRadius = 320.0f;



        //HERE: this should all be done elsewhere - either exposed to the editor, or loaded in from the DB or from a file.
        mD.mSkyboxPath = Application.dataPath + "/TerrainMaster/Skybox/";//Currently the same
        mD.mTerrainPath = Application.dataPath + "/TerrainMaster/NewTerrain/";//but could be different. 
        mD.mResourcePath = Application.dataPath + "/Resources/Terrain/";

        mD.mSquareSize = 10.0f;
        mD.mSkyboxRes = 800;
        mD.mHeightmapRes = 257;//256, FiX FIX FIX, have to get back into FG to fix this though.

        mD.mTileWidth = (float)(mD.mHeightmapRes - 1) * mD.mSquareSize;

        mD.mMapCenterLatitude = 44.0f;// 21.936f;//22.0f;////HERE: this is the geographic center of the whole map.
        mD.mMapCenterLongitude = -123.0046f;// 123.0047f;//  -159.380f;//-159.5f;//GET THIS FROM THE GUI! //.005?? Something is broken, by just five thousandths. ?? FIX FIX FIX

        //mD.mMetersPerDegreeLongitude = 80389.38609f;//2560.0f / mTileWidthLongitude ;//FIX: get from server, based on centerLat/Long
        //mD.mDegreesPerMeterLongitude = 0.000012439f;//mTileWidthLongitude / 2560.0f;
        //mD.mMetersPerDegreeLatitude = 111169.0164f;//2560.0f / mTileWidthLatitude ;
        //mD.mDegreesPerMeterLatitude = 0.000008995f;//mTileWidthLatitude / 2560.0f;
        float rLat = mD.mMapCenterLatitude * Mathf.Deg2Rad;
        mD.mMetersPerDegreeLatitude = 111132.92f - 559.82f * Mathf.Cos(2 * rLat) + 1.175f * Mathf.Cos(4 * rLat);
        mD.mMetersPerDegreeLongitude = 111412.84f * Mathf.Cos(rLat) - 93.5f * Mathf.Cos(3 * rLat);
        mD.mDegreesPerMeterLongitude = 1.0f / mD.mMetersPerDegreeLongitude;
        mD.mDegreesPerMeterLatitude = 1.0f / mD.mMetersPerDegreeLatitude;
        mD.mTileWidthLongitude = mD.mDegreesPerMeterLongitude * mD.mTileWidth;
        mD.mTileWidthLatitude = mD.mDegreesPerMeterLatitude * mD.mTileWidth;
        Debug.Log("MetersPerDegree Longitude: " + mD.mMetersPerDegreeLongitude + " Latitude " + mD.mMetersPerDegreeLatitude + " degreesPerMeterLong " + mD.mDegreesPerMeterLongitude);
        mD.mClientPosLongitude = -9999.0f;//Doing this as a flag to tell us we haven't done init yet - since 0.0 degrees longitude is possible. 
        mD.mClientPosLatitude = 0.0f;//Is there a good reason these are in this struct though?
        mD.mClientPosAltitude = 0.0f;


        //Smaller units for loading forest, streets, or shapes.
        mCellGridSize = 32;

        //Yup, this is how you start external processes, just fyi. Not that I need to do that right now.
        //System.Diagnostics.Process.Start("C:/Megamotion/Torque3D/My Projects/Megamotion/game/Megamotion.exe");

        findClientTile();

        string tileName = getTileName(mTileStartLongitude, mTileStartLatitude);
        string heightfilename = mD.mTerrainPath + "hght." + tileName + ".bin";// sprintf(heightfilename, "%shght.%s.bin", mD.mTerrainPath.c_str(), tileName);
        string texturefilename = mD.mTerrainPath + "text." + tileName + ".bin";// sprintf(texturefilename, "%stext.%s.bin", mD.mTerrainPath.c_str(), tileName);
        TerrainData terrData;
        int alphaRes;

        Debug.Log("Client Tile: " + tileName);
        //GridSize is the number of terrains you can fit within the tileLoadRadius from the center, so basically (2 * loadRadius)/tileWidth, 
        //Except, we want to limit gridsize to odd numbers, eg 3x3, 5x5, 7x7 etc. so that there will always be a center tile. 

        mGridSize = 1 + (2 * ((int)(mD.mTileLoadRadius / mD.mTileWidth) + 1));
        mGridMidpoint = (mGridSize - 1) / 2;
        mTerrainGrid = new List<GameObject>();//mGridSize * mGridSize
        mTerrains = new List<GameObject>();
        mTerrainCoords = new List<Coordinates>();
        //mLoadedFeatures = new List<int>();
        //mRoadedTerrains = new List<int>();
        //First, fill up the mTerrainGrid array with blank GameObjects so we can reference any point in the array without hitting a null.
        for (int y = 0; y < mGridSize; y++)
            for (int x = 0; x < mGridSize; x++)
                mTerrainGrid.Add(null);

        //Next, let's make sure we have a BaseTerrainObject, and if we do, make that mTerrain and also the center square of mTerrainGrid.
        if (BaseTerrainObject != null)
        {
            BaseTerrain = BaseTerrainObject.GetComponent<Terrain>();
            if (BaseTerrain != null)
            {
                BaseTerrainObject.name = tileName;
                terrData = BaseTerrain.terrainData;
                mTerrain = BaseTerrainObject;
                int index = (((int)(mGridSize / 2) * mGridSize) + ((int)(mGridSize / 2)));
                mTerrainGrid[index] = BaseTerrainObject;//(the center square)
                mTerrains.Add(BaseTerrainObject);
                mTerrainCoords.Add(new Coordinates((double)mTileStartLongitude, (double)(mTileStartLatitude)));
                loadTerrainData(mTerrains.Count-1, heightfilename, texturefilename, tileName);
                //Debug.Log("Trying to add forest and roads for base terrain, data name: " + BaseTerrain.terrainData.name);
                alphaRes = terrData.alphamapResolution;
                tSplatmap = terrData.GetAlphamaps(0, 0, alphaRes, alphaRes);                
            }
        }
        else
        {
            Debug.Log("Base Terrain object not found!!!!");
            //Here: make a message to the user and exit.
        }

        //loadTileGrid();
        mLoadStage = PagerLoadStage.NothingLoaded;

        Debug.Log("Made terrainGrid, length " + mTerrainGrid.Count + " gridSize " + mGridSize + " terrains count: " + mTerrains.Count);

        if (mUseDataSource)
        {
            mDataSource = new worldDataSource(false);
            //mDataSource = new dataSource(false);
            Debug.Log("new mDatasource!!! ... server = " + mDataSource.mServer.ToString());
            mDataSource.Start();
        }
        
        float endLat = mTileStartLatitude + mD.mTileWidthLatitude;
        float endLong = mTileStartLongitude + mD.mTileWidthLongitude;
        terrData = mTerrain.GetComponent<Terrain>().terrainData;
        alphaRes = terrData.alphamapResolution;
        tSplatmap = terrData.GetAlphamaps(0, 0, alphaRes, alphaRes);


        /*  //TEMP, ROADS
        ////////////////////////////////////////////////////////////////////////
        //TEMP TEMP TEMP load an array from the db!
        // create a new road type - FIX: define set of roadProperties elsewhere and load these up in a loop.
        roadTypeHwy = new ERRoadType();
        roadTypeHwy.roadWidth = 5.0f;
        roadTypeHwy.roadMaterial = Resources.Load("Materials/roads/road material") as Material;
        roadTypeHwy.layer = 1;
        roadTypeHwy.tag = "Untagged";
        roadTypeHwy.terrainDeformation = true;
        //roadTypeHwy.sidewalks = true;
        //roadTypeHwy.sidewalkWidth = 1.5f;

        roadTypeMotorway = new ERRoadType();
        roadTypeMotorway.roadWidth = 10.0f;
        roadTypeMotorway.roadMaterial = Resources.Load("Materials/roads/road material") as Material;
        roadTypeMotorway.layer = 1;
        roadTypeMotorway.tag = "Untagged";
        roadTypeMotorway.terrainDeformation = true;

        roadTypeDirt = new ERRoadType();
        roadTypeDirt.roadWidth = 4.0f;
        roadTypeDirt.roadMaterial = Resources.Load("Materials/roads/dirt material") as Material;
        roadTypeDirt.layer = 1;
        roadTypeDirt.tag = "Untagged";
        roadTypeDirt.terrainDeformation = true;

        roadTypeSidewalk = new ERRoadType();
        roadTypeSidewalk.roadWidth = 1.5f;
        roadTypeSidewalk.roadMaterial = Resources.Load("Materials/sidewalks/sidewalk") as Material;
        roadTypeSidewalk.layer = 1;
        roadTypeSidewalk.tag = "Untagged";
        roadTypeSidewalk.terrainDeformation = true;

        roadTypePath = new ERRoadType();
        roadTypePath.roadWidth = 1.5f;
        roadTypePath.roadMaterial = Resources.Load("Materials/roads/Dirt Grass") as Material;
        roadTypePath.layer = 1;
        roadTypePath.tag = "Untagged";
        roadTypePath.terrainDeformation = true;

        X_SourceConn = mRoadNetwork.GetSourceConnectionByName("Default X Crossing");
        T_SourceConn = mRoadNetwork.GetSourceConnectionByName("Default T Crossing");
        ////////////////////////////////////////////////////////////////////////

        if (importRoads) ImportOpenStreetMap("Assets/OSM/cottage_grove.osm");

        if (convertRoads) ConvertRoads(new Vector2(mTileStartLongitude, mTileStartLatitude),new Vector2(endLong, endLat),0);
        if (makeRoads) MakeRoads(new Vector2(mTileStartLongitude, mTileStartLatitude), new Vector2(endLong, endLat), 0);
        */
        MakeShapes(BaseTerrain);
        if (terrData.treeInstanceCount == 0)
            MakeForest(BaseTerrain);


        //EDITOR ONLY
        //if (Application.isEditor)
        //    AssetDatabase.SaveAssets();//EDITOR ONLY
                                       //FIX: Apparently this can't even compile in a build. Is there an #ifdef way to get rid of it?


    }

    private void OnDestroy()
    {
        if (mDbCmd != null)
        {
            mDbCmd.Dispose();
            mDbCmd = null;
        }
        if (mDbConn != null)
        {
            mDbConn.Close();
            mDbConn = null;
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("TerrainPager update!");
        bool newTile = false;

        //First, establish current position.
        findClientTile();   //Hm, every tick, really?

        //Debug.Log("Terrain pager ticking, client pos: " + mClientPos.ToString() + 
        //       " long  " + mD.mClientPosLongitude.ToString() + " lat " + mD.mClientPosLatitude.ToString());

        if (mClientPos.magnitude == 0.0)
        {//Check to make sure we have a reasonable player location. [NOTE: Do *not* make player spawn at perfect (0,0,0)!]
            return;//No player yet.
        }

        /* /////////// TESTING - and yes, this works. ////////////
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("P KEY DOWN");
            TerrainData tData = BaseTerrain.terrainData;
            SplatPrototype[] splatPrototypes = tData.splatPrototypes;
            int alphaRes = tData.alphamapResolution;
            float[,,] data = tData.GetAlphamaps(0, 0, alphaRes, alphaRes);
            for (int i = 0; i < alphaRes; i++)
            {
                for (int j = 0; j < alphaRes; j++)
                {
                    data[j, i, 0] = 0.0f;
                    data[j, i, 1] = 0.0f;
                    if (i < alphaRes/2)
                    {
                        data[j, i, 2] = 1.0f;
                        data[j, i, 3] = 0.0f;
                    } else  {
                        data[j, i, 2] = 0.0f;
                        data[j, i, 3] = 1.0f;
                    }
                    data[j, i, 4] = 0.0f;
                    data[j, i, 5] = 0.0f;
                }
            }
            tData.SetAlphamaps(0, 0, data);
        }*/

        if ((mUseDataSource) && (mDataSource.mCurrentTick <= 500))
        {
            Debug.Log("mDatasource ticking... " + mDataSource.mCurrentTick.ToString());
            mDataSource.tick();
        }
        
        switch (mLoadStage)
        {
            case PagerLoadStage.NothingLoaded:  //First pass, just found our first useful client position.

                //Con::printf("terrainPager first client info: long/lat %f %f, pos %f %f",
                //            mD.mClientPosLongitude, mD.mClientPosLatitude, mClientPos.x, mClientPos.y);

                mLastTileStartLong = mTileStartLongitude;
                mLastTileStartLat = mTileStartLatitude;

                if (!mUseDataSource) //If not using data source, just page around the finished terrains.
                {
                    mLoadStage = PagerLoadStage.PagerRunning;//10 is the holding pattern loop.

                    //loadTileGrid();
                    checkTileGrid();
                }
                else
                {
                    mLoadStage = PagerLoadStage.DataSourceConnected;
                }
                break;

            case PagerLoadStage.DataSourceConnected:

                if (mDataSource.mReadyForRequests)
                {
                    mDataSource.addInitTerrainRequest();// (&mD, mD.mTerrainPath.c_str());
                    mDataSource.addInitSkyboxRequest();// (mD.mSkyboxRes, 0, mD.mSkyboxPath.c_str());
                    mSentInitRequests = true;
                    mLoadStage = PagerLoadStage.DataSourceReady;
                }
                break;

            case PagerLoadStage.DataSourceReady:             // data for anything it can't find locally.
                //loadTileGrid();
                break;

            case PagerLoadStage.DataRequested:
                if (mDataSource.mTerrainDone == true)
                {
                    mLoadStage = PagerLoadStage.DataSourceReady;
                }
                else
                {
                    //Con::printf("TerrainPager waiting for terrain data...");
                    if ((int)mTerrainRequestTick < ((int)mCurrentTick - (int)mSkyboxTickInterval))
                    {
                        mLoadStage = PagerLoadStage.PagerRunning;//Give up on this load, it got lost in transmission. (?)
                    }
                }
                break;
            //else if (mLoadState==4) //Waiting for skybox images.
            //{
            //	if (mDataSource->mSkyboxDone==true)
            //	{
            //		Con::printf("reloading skybox!!!!");
            //		reloadSkyboxImages(); //Rotate and flip the raw images from Flightgear to make them work in T3D skybox material.
            //		mSentSkyboxRequest = false;
            //		mLastSkyboxTick = mCurrentTick;
            //		mLoadState=5;
            //	} else {
            //		Con::printf("TerrainPager waiting for skybox images...");
            //	}
            //}
            case PagerLoadStage.SkyboxDataAvailable:

                //if ((mCurrentTick - mLastSkyboxTick) > mSkyboxLoadDelay)
                //{
                //Hmm, trying this again, with no delay except one tick, is that enough?
                //Con::printf("updating skybox!!!!!!!!!!");
                //Con::executef(this, "UpdateSkybox");
                mLoadStage = PagerLoadStage.PagerRunning;
                //}
                break;

            case PagerLoadStage.PagerRunning:
                if (mLastTileStartLong != mTileStartLongitude) newTile = true;
                if (mLastTileStartLat != mTileStartLatitude) newTile = true;
                if (newTile)
                {
                    checkTileGrid();

                    mLastTileStartLong = mTileStartLongitude;
                    mLastTileStartLat = mTileStartLatitude;

                    mTerrain = mTerrainGrid[(mGridMidpoint * mGridSize) + mGridMidpoint];

                }
                //else
                //{
                    //if (mCurrentTick++ % mTickInterval == 0) //Hmm, might want to rename mTickInterval to mTileGridInterval, this is the only place it's used.
                        //checkTileGrid();
                //}

                //Con::printf("terrain pager load state: %d sendControls %d skyboxInterval %d currentTick %d",
                //	mLoadState,mDataSource->mSendControls,mSkyboxTickInterval,mCurrentTick);

                if ((mUseDataSource) && (mDataSource.mSendControls == 1) &&
                    ((int)mLastSkyboxTick < ((int)mCurrentTick - (int)mSkyboxTickInterval)) &&
                    (mSentSkyboxRequest == false))
                {
                    mDataSource.addSkyboxRequest(mTileStartLongitude, mTileStartLatitude, mD.mClientPosLongitude, mD.mClientPosLatitude, mD.mClientPosAltitude);
                    //mLoadState = 4;
                    mSentSkyboxRequest = true;
                    mDataSource.mSkyboxDone = false;
                    //mLastSkyboxTick = mCurrentTick;
                    mSkyboxRequestTick = mCurrentTick;
                    //Con::printf("adding a skybox request! ");
                }
                else if ((mSentSkyboxRequest) && //Either we're done, or we've waited too long and should give up on this one.
                        ((mDataSource.mSkyboxDone) || ((int)mSkyboxRequestTick < ((int)mCurrentTick - (int)mSkyboxTickInterval))))
                {
                    //Con::printf("reloading skybox!!!!");
                    //reloadSkyboxImages();
                    mSentSkyboxRequest = false;
                    mLastSkyboxTick = mCurrentTick;
                    mLoadStage = PagerLoadStage.SkyboxDataAvailable;//??
                }
                break;
                //if (((mCurrentTick - mLastForestTick) > mForestTickInterval))//mDoForest && - now checking doForest later, so we can do streets w/o forest
                //    checkForest();        
        }
    }
    
    void OpenDatabase()
    {        
        string conn = "URI=file:" + Application.dataPath + "/" + mDbPath;//Will this break on build as well? Move to Resources?
        mDbConn = (IDbConnection)new SqliteConnection(conn);
        mDbConn.Open(); //Open connection to the database.
        mDbCmd = mDbConn.CreateCommand();

        //Load shapeFile prefabs
        //WARNING: FIX FIX FIX - this is loading the entire mapShapeFile, with no regard for what we are actually going to be using in this area
        string sqlQuery = "SELECT id, path FROM mapShapeFile;";//This could be a big waste of memory in the future.
        mDbCmd.CommandText = sqlQuery;
        IDataReader reader = mDbCmd.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            string path = reader.GetString(1);
            UnityEngine.Object obj = Resources.Load(path);
            mPrefabs.Add(id, obj);
            //Debug.Log("id= " + id + "  path =" + path);
        }
        reader.Close();

    }
    
    private void MakeShapes(Terrain terr)
    {
        TerrainData terrData = terr.terrainData;
        string selectQuery;

        Coordinates terrCoord = new Coordinates();
        Coordinates nodeCoord = new Coordinates();
        
        string tileName = terrData.name.Substring(terrData.name.IndexOf('.') + 1);
        if (tileName.IndexOf('(') > 0)  //Getting "(Clone) in our tilenames...???
            tileName = tileName.Remove(tileName.IndexOf('('));
        string objName = "";
        int nodeId = 0;

        selectQuery = "SELECT id,latitude,longitude FROM mapNode WHERE name='" + tileName + "' AND type='Terrain';";//type='Terrain' is probably overkill, but why not.
        mDbCmd.CommandText = selectQuery;
        IDataReader reader = mDbCmd.ExecuteReader();
        //Debug.Log(selectQuery);
        while (reader.Read())
        {
            //Playing fast and loose here but I KNOW I gave everybody a latitude and longitude...            
            terrCoord.latitude = reader.GetDouble(reader.GetOrdinal("latitude"));
            terrCoord.longitude = reader.GetDouble(reader.GetOrdinal("longitude"));
        }
        reader.Close();
        
        selectQuery = "SELECT id,latitude,longitude,name FROM mapNode WHERE type='MapNode' AND latitude >= " + terrCoord.latitude +
                      " AND latitude < " + (terrCoord.latitude + mD.mTileWidthLatitude) + " AND longitude >= " +
                      terrCoord.longitude + " AND longitude < " + (terrCoord.longitude + mD.mTileWidthLongitude) + ";";
        //Debug.Log("makeShapes: " + selectQuery);
        mDbCmd.CommandText = selectQuery;
        reader = mDbCmd.ExecuteReader();
        while (reader.Read())
        {
            //Playing fast and loose here but I KNOW I gave everybody a latitude and longitude...
            nodeId = reader.GetInt32(reader.GetOrdinal("id"));
            nodeCoord.latitude = reader.GetDouble(reader.GetOrdinal("latitude"));
            nodeCoord.longitude = reader.GetDouble(reader.GetOrdinal("longitude"));
            try { objName = reader.GetString(reader.GetOrdinal("name")); }
            catch { }

            GameObject myNode = GameObject.Instantiate(MapNodePrefab);
            Vector3 myPos = new Vector3();
            myPos = ConvertLatLongToXYZ(nodeCoord.longitude,0, nodeCoord.latitude);
            myPos.y = terr.SampleHeight(myPos);
            myNode.transform.position = myPos;
            myNode.GetComponent<SimBase>().id = nodeId;
            //.y = terrData.GetHeight();
            myNode.name = objName;


            string shapeQuery = "SELECT * FROM mapShape WHERE node_id=" + nodeId + ";";
            //Debug.Log(shapeQuery);
            IDbCommand mDbCmd2 = mDbConn.CreateCommand();
            mDbCmd2.CommandText = shapeQuery;
            IDataReader subReader = mDbCmd2.ExecuteReader();
            while (subReader.Read())
            {
                Vector3 pos, scale;
                Quaternion q;
                int id;
                int file_id;

                try
                {
                    float x, y, z, rx, ry, rz, rw, sx, sy, sz;
                    id = subReader.GetInt32(subReader.GetOrdinal("id"));
                    file_id = subReader.GetInt32(subReader.GetOrdinal("file_id"));
                    x = subReader.GetFloat(subReader.GetOrdinal("x"));
                    y = subReader.GetFloat(subReader.GetOrdinal("y"));
                    z = subReader.GetFloat(subReader.GetOrdinal("z"));
                    rx = subReader.GetFloat(subReader.GetOrdinal("rx"));
                    ry = subReader.GetFloat(subReader.GetOrdinal("ry"));
                    rz = subReader.GetFloat(subReader.GetOrdinal("rz"));
                    rw = subReader.GetFloat(subReader.GetOrdinal("rw"));
                    sx = subReader.GetFloat(subReader.GetOrdinal("sx"));
                    sy = subReader.GetFloat(subReader.GetOrdinal("sy"));
                    sz = subReader.GetFloat(subReader.GetOrdinal("sz"));
                    pos = new Vector3(x, y, z);
                    q = new Quaternion(rx, ry, rz, rw);
                    scale = new Vector3(sx, sy, sz);
                }
                catch
                {
                    Debug.Log("There was a problem loading shapes for mapNode " + nodeId);
                    continue;
                }

                //NOW, we have pos, rot and scale, and file id. I could have done this in the one query above but not sure how to mix joins with select *
                //string pathQuery = "SELECT path FROM mapShapeFile WHERE id=" + file_id + ";";
                //Debug.Log(pathQuery);
                //IDbCommand mDbCmd3 = mDbConn.CreateCommand();
                //mDbCmd3.CommandText = pathQuery;
                //IDataReader subSubReader = mDbCmd3.ExecuteReader();
                //subSubReader.Read();
                //string path = subSubReader.GetString(subSubReader.GetOrdinal("path"));
                //subSubReader.Close();
                //subSubReader.Dispose();
                //mDbCmd3.Dispose();

                //if (path.Length == 0)
                //    continue;

                //And now we have the path, since I did *not* do a join in the original query for lazy and stupid reasons. (FIX FIX FIX)
                ////UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath(path + ".prefab", typeof(GameObject));
                ////GameObject obj = Instantiate(prefab) as GameObject;                
                GameObject obj = Instantiate(mPrefabs[file_id]) as GameObject;
                Vector3 objPos = myNode.transform.position + pos;
                objPos.y = terr.SampleHeight(objPos);
                obj.transform.position = objPos;
                obj.transform.rotation = q; // Ah, hmm, as currently recorded, map nodes don't have any rotation. This might suck.
                obj.transform.localScale = scale;//Ditto with scale.
            }
            subReader.Close();
            subReader.Dispose();
            mDbCmd2.Dispose();
        }
        reader.Close();

        
        /*  // HERE: New query - select from mapShape where node_id = 
         *  
         *  
        





            //Playing fast and loose here but I KNOW I gave everybody a latitude and longitude...
            objCoord.latitude = reader.GetDouble(reader.GetOrdinal("latitude"));
            objCoord.longitude = reader.GetDouble(reader.GetOrdinal("longitude"));
            try { objName = reader.GetString(reader.GetOrdinal("name")); }
            catch { }

            GameObject myNode = GameObject.Instantiate(MapNodePrefab);
            Vector3 myPos = new Vector3();
            myPos = ConvertLatLongToXYZ(objCoord.longitude, 0, objCoord.latitude);
            myPos.y = terr.SampleHeight(myPos);
            myNode.transform.position = myPos;
            //.y = terrData.GetHeight();
            Debug.Log("!!!!!!!!!!!!!!! " + tileName + " INStANTIATED a mapnode!!!!! " + objName + " pos " + myNode.transform.position + "!!!!!!!!!!!!!");
            myNode.name = objName;


        
        */


    }

    //For now let's just use the one DB, but not sure which way it'll go in the future.
    public void ImportOpenStreetMap(string osm_file)//, string db)
    {
        Debug.Log("Import OpenStreetMap!!!!!!!!    " + osm_file);

        if (mDbCmd == null)
            return;

        XmlNode boundsNode;
        XmlDocument osmDocument = new XmlDocument();
        osmDocument.Load(osm_file);
        string osmVersion = "";
        string cellName,insertQuery,updateQuery,idQuery, lastIdQuery;

        lastIdQuery = "SELECT last_insert_rowid();";

        boundsNode = osmDocument.DocumentElement.ChildNodes[0];
        if (boundsNode.Name.Equals("bounds"))
        {
            double minLat = Convert.ToDouble(boundsNode.Attributes["minlat"]?.InnerText);
            double minLon = Convert.ToDouble(boundsNode.Attributes["minlon"]?.InnerText);
            double maxLat = Convert.ToDouble(boundsNode.Attributes["maxnlat"]?.InnerText);
            double maxLon = Convert.ToDouble(boundsNode.Attributes["maxnlon"]?.InnerText);
        }

        //Debug.Log("OSM doc: " + osmDocument.Name + " doc version " + osmDocument.Attributes["version"]?.InnerText);
        foreach (XmlNode node in osmDocument.DocumentElement.ChildNodes)
        {
            string nodeName = node.Name;
            if (nodeName.Equals("node"))
            {
                long osmId = Convert.ToInt64(node.Attributes["id"]?.InnerText);
                double latitude  = Convert.ToDouble(node.Attributes["lat"]?.InnerText);
                double longitude = Convert.ToDouble(node.Attributes["lon"]?.InnerText);
                //Get cell id somehow... possibly by lookup array usig lat/long.
                
                int nodeId = 0;
                idQuery = "SELECT id FROM mapNode WHERE osm_id=" + osmId.ToString() + ";";
                mDbCmd.CommandText = idQuery;
                IDataReader reader = mDbCmd.ExecuteReader();
                while (reader.Read())
                {
                    try { nodeId = reader.GetInt32(0); }
                    catch (IndexOutOfRangeException) { nodeId = 0; }
                }
                reader.Close();
                if (nodeId == 0)
                {
                    insertQuery = "INSERT INTO mapNode (osm_id,latitude,longitude) VALUES (" + osmId.ToString() +
                                    "," + latitude.ToString() + "," + longitude.ToString() + ");";// ON CONFLICT (osm_id) DO NOTHING;";
                    mDbCmd.CommandText = insertQuery;
                    mDbCmd.ExecuteNonQuery();
                }
                Debug.Log("node type: " + nodeName + " latitude " + latitude + " longitude " + longitude);                
            }
            else if (nodeName.Equals("way"))
            {
                string osmIDsString = "";
                long osmId = Convert.ToInt64(node.Attributes["id"]?.InnerText);
                //WHOOPS! Gotta fix this upsert with another query, because ON CONFLICT only works on unique fields, and we need osm_id to allow nulls.
                //insertQuery = "INSERT INTO mapFeature (osm_id) VALUES (" + osmId.ToString() + 
                //            ") ON CONFLICT (osm_id) DO NOTHING;";

                int featureId = 0;
                idQuery = "SELECT id FROM mapFeature WHERE osm_id=" + osmId.ToString() + ";";
                mDbCmd.CommandText = idQuery;
                IDataReader reader = mDbCmd.ExecuteReader();
                while (reader.Read())
                {
                    try { featureId = reader.GetInt32(0); }
                    catch (IndexOutOfRangeException) { featureId = 0; }
                }
                reader.Close();
                if (featureId == 0)
                {
                    insertQuery = "INSERT INTO mapFeature (osm_id) VALUES (" + osmId.ToString() + ");";
                    mDbCmd.CommandText = insertQuery;
                    mDbCmd.ExecuteNonQuery();

                    idQuery = "SELECT id FROM mapFeature WHERE osm_id=" + osmId + ";";
                    mDbCmd.CommandText = idQuery;
                    reader = mDbCmd.ExecuteReader();
                    reader.Read();
                    featureId = reader.GetInt32(0);
                    reader.Close();
                }
                //Debug.Log("Upserted a feature! ID " + featureId + " childNodes " + node.ChildNodes.Count);
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    if (childNode.Name.Equals("nd"))
                    {
                        long nodeOsmId = Convert.ToInt64(childNode.Attributes["ref"]?.InnerText);//Hm, what's the easy simple fast way to get the corresponding node id
                        insertQuery = "INSERT INTO mapFeatureNode (feature_id,node_osm_id) VALUES (" + featureId + "," + nodeOsmId + ");";//  for this osm id?
                        mDbCmd.CommandText = insertQuery;
                        mDbCmd.ExecuteNonQuery();
                    }
                    else if (childNode.Name.Equals("tag"))
                    {
                        string type = childNode.Attributes["k"]?.InnerText;
                        string subtype = childNode.Attributes["v"]?.InnerText.Replace("'", "''");//Escape the single quotes
                        updateQuery = "";

                        if ( type.Equals("highway") || type.Equals("waterway") || type.Equals("building") ) //Add more later...                        
                            updateQuery = "UPDATE mapFeature SET type='" + type + "', subtype='" + subtype + "' WHERE id=" + featureId + ";";   
                        else if ( type.Equals("service") )                        
                            updateQuery = "UPDATE mapFeature SET subsubtype='" + subtype + "' WHERE id=" + featureId + ";";
                        else if ( type.Equals("name") )                        
                            updateQuery = "UPDATE mapFeature SET name='" + subtype + "' WHERE id=" + featureId + ";";                        

                        if (updateQuery.Length  > 0)
                        {
                            Debug.Log(updateQuery);
                            mDbCmd.CommandText = updateQuery;
                            mDbCmd.ExecuteNonQuery();
                        }

                    }
                }
                //if (node.ChildNodes.Count > 0)
                //{
                    //string cleanOsmIDs = osmIDsString.Remove(osmIDsString.Length - 1);//Remove final comma.
                    //updateQuery = "UPDATE mapNode SET feature_id=" + featureId + " WHERE osm_id IN (" + cleanOsmIDs + ");";
                    //Debug.Log(updateQuery);
                   // mDbCmd.CommandText = updateQuery;
                    //mDbCmd.ExecuteNonQuery();
                //}
            }            
        }        
    }
    /*
    void TerrainPager::loadOSM(const char* xml_file, const char* map_db)
{
	
	if (!mSQL->OpenDatabase(map_db))
		return;
	
	mDbPath = map_db;

	std::ostringstream insert_query;
    char select_query[512], update_query[255], total_queries[56000];
    int id, result, total_query_len = 0;

    bool findingTag, foundTag;
    sqlite_resultset* resultSet;
    char cellName[20];
    char nodeId[16];//Have to do this because we are STILL USING VS2010. They added atoll() for long longs in 2011
    char wayId[16];             //But it's okay, because sqlite can convert the char string for us.

    SimXMLDocument* doc = new SimXMLDocument();
    doc->registerObject();

    S32 loaded = doc->loadFile(xml_file);
	if (loaded) 
	{
		bool osmLoad = false;
    bool osmBounds = false;

    F32 version, minlat;
    double nodeLat, nodeLon;
    //long nodeId;		

    osmLoad = doc->pushFirstChildElement("osm");
		if (doc->attributeExists("version") )
			version = atof(doc->attribute("version"));

		osmBounds = doc->pushFirstChildElement("bounds");
		if (doc->attributeExists("minlat") )
			minlat = atof(doc->attribute("minlat"));

		Con::printf("opened the document %s, osm %d version %f bounds %d minLat %f",xml_file,osmLoad,version,osmBounds,minlat);

		//sprintf(insert_query,"BEGIN;\n");
		insert_query << "BEGIN;";
		result = mSQL->ExecuteSQL(insert_query.str().c_str());
    insert_query.clear(); insert_query.str("");
		//Con::printf("result %d: %s",result,insert_query);

		while(doc->nextSiblingElement("node"))
		{
			//nodeId = atol(doc->attribute("id"));//AAAH... turns out what we need is atoll(), and it doesn't happen until 2011
			sprintf(nodeId,"%s", doc->attribute("id"));
			nodeLat = atof(doc->attribute("lat"));
			nodeLon = atof(doc->attribute("lon"));
			Point3F nodePos = convertLatLongToXYZ(nodeLon, nodeLat, 0.0);
    getCellName(nodePos, cellName);

    //sprintf(insert_query,"INSERT INTO osmNode (osm_id,latitude,longitude,cell_name) VALUES (%d,%f,%f,'%s');\n",
    //	nodeId,nodeLat,nodeLon,cellName);
    insert_query.precision(8);
			insert_query << "INSERT INTO osmNode (osm_id,latitude,longitude,cell_name) VALUES (CAST('" << nodeId << "' AS INTEGER)," << 
									nodeLat << "," << nodeLon << ",'" << cellName << "');";
			result = mSQL->ExecuteSQL(insert_query.str().c_str());
    Con::printf("node lat %f long %f insert query: %s",nodeLat,nodeLon,insert_query.str().c_str());
			insert_query.clear(); insert_query.str("");		
			foundTag = false;
			findingTag = doc->pushFirstChildElement("tag");
    foundTag = findingTag;
			while (findingTag)
			{				
				if (!strcmp(doc->attribute("k"),"name"))
				{//HERE: we REALLY need that escape single quotes function, if not an escape every possible oddity function
					std::string name = doc->attribute("v");
    std::string escapedName = escapeSingleQuotes(&name);
    insert_query << "UPDATE osmNode SET name='" << escapedName.c_str() << "' WHERE osm_id=CAST('" << nodeId << "' AS INTEGER);";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
    Con::printf("updated node name: %s",insert_query.str().c_str());
					insert_query.clear(); insert_query.str("");

				} else if (!strcmp(doc->attribute("k"),"highway")) {
					insert_query << "UPDATE osmNode SET type='" << doc->attribute("v") << "' WHERE osm_id=CAST('" << nodeId << "' AS INTEGER);";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

				} else if (!strcmp(doc->attribute("k"),"building") && !strcmp(doc->attribute("v"),"yes")) {
					insert_query << "UPDATE osmNode SET type='building' WHERE osm_id=CAST('" << nodeId << "' AS INTEGER);";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");
				}
				findingTag = doc->nextSiblingElement("tag");
			}			
			if (foundTag) doc->popElement();
		}

		doc->popElement();

bool findingMyWay = doc->pushFirstChildElement("way");
		while(findingMyWay)
		{
			sprintf(wayId,"%s", doc->attribute("id"));
			insert_query << "INSERT INTO osmWay (osm_id) VALUES (CAST('" << wayId << "' AS INTEGER));";
			//Con::printf("result %d: %s",result,insert_query);
			result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

			bool findingNd = doc->pushFirstChildElement("nd");
			while(findingNd)
			{
				sprintf(nodeId,"%s", doc->attribute("ref"));

				//sprintf(insert_query,"INSERT INTO osmWayNode (way_id,node_id) VALUES (%d,%d);\n",wayId,nodeId);
				insert_query << "INSERT INTO osmWayNode (way_id,node_id) VALUES (CAST('" << wayId << "' AS INTEGER),CAST('" << nodeId << "' AS INTEGER));";
				Con::printf("result %d: %s",result,insert_query.str().c_str());
				result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

				findingNd = doc->nextSiblingElement("nd");
			}
			doc->popElement();
bool foundTag = false;
bool findingTag = doc->pushFirstChildElement("tag");
foundTag = findingTag;
			while (findingTag)
			{				
				Con::printf("tag %s : %s",doc->attribute("k"),doc->attribute("v"));

				if (!strcmp(doc->attribute("k"),"name"))
				{
					//sprintf(insert_query,"UPDATE osmWay SET name='%s' WHERE osm_id=%d;\n",doc->attribute("v"),way_id);
					std::string name = doc->attribute("v");
std::string escapedName = escapeSingleQuotes(&name);
insert_query << "UPDATE osmWay SET name='" << escapedName.c_str() << "' WHERE osm_id=CAST('" << wayId << "' AS INTEGER);";
					
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

				} else if (!strcmp(doc->attribute("k"),"highway")) {
					//sprintf(insert_query,"UPDATE osmWay SET type='%s' WHERE osm_id=%d;\n",doc->attribute("v"),wayId);
					insert_query << "UPDATE osmWay SET type='" << doc->attribute("v") << "' WHERE osm_id=CAST('" << wayId << "' AS INTEGER);";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

				} else if (!strcmp(doc->attribute("k"),"building") && !strcmp(doc->attribute("v"),"yes")) {
					//sprintf(insert_query,"UPDATE osmWay SET type='building' WHERE osm_id=%d;\n",wayId);
					insert_query << "UPDATE osmWay SET type='building' WHERE osm_id=CAST('" << wayId << "' AS INTEGER);";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");
				}

				findingTag = doc->nextSiblingElement("tag");
			}				
			if (foundTag) doc->popElement();
findingMyWay = doc->nextSiblingElement("way");
		}

		//sprintf(insert_query,"COMMIT;\n");
		insert_query << "COMMIT;";
		result = mSQL->ExecuteSQL(insert_query.str().c_str());
insert_query.clear(); insert_query.str("");

	} else Con::errorf("Failed to load OpenStreetMap export file: %s",xml_file);

	doc->deleteObject();

mSQL->CloseDatabase();

	return;
}*/

    private void saveStaticShapes()
    {
        /*
        SimSet* missionGroup = NULL;
	missionGroup = dynamic_cast<SimSet*>(Sim::findObject("MissionGroup"));

	unsigned long startTime =  clock();

	if (missionGroup == NULL)
		Con::printf("No mission group found.");

	if (!mSQL)
		Con::printf("mSQL is not valid");

	if ((mSQL)&&(missionGroup))
	{
		int nodeId,shapeId,fileId,posId,rotId,scaleId;
		int result,total_query_len=0;
		double nodeLong,nodeLat;
		std::string shapeName;
		std::ostringstream insert_query,update_query,file_id_query,scale_id_query;
		sqlite_resultset *resultSet;
		Vector3 pos,latLongPos,scale;
		Vector2 cellPos;
		char cellName[20];

		//for(SimSet::iterator itr=missionGroup->begin(); itr!=missionGroup->end(); ++itr)
		for(SimSetIterator itr(missionGroup); *itr; ++itr)
      {
         TSStatic* object = dynamic_cast< TSStatic* >( *itr );
			if (object)
			{
				pos = object->getPosition();
				latLongPos = convertXYZToLatLong(pos);
				cellPos = getCellCoords(pos);
				getCellName(cellPos.x,cellPos.y,cellName);
				Con::printf("TerrainPager::saveStaticShapes found a static. %s %f %f %f id %d",object->mShapeName,pos.x,pos.y,pos.z,object->mOseId); 

				//FIRST: find out if this static is already in the database! I guess this means adding oseId to TSStatic.
				//So, now we have an mOseId in TSStatic, and we can set that here and check if it has a value.
				if (object->mOseId > 0)
				{
					if (object->mIsDirty)
					{
						int updateStart = clock();
						//SO, now that we have added mIsDirty to TSStatic, and set it in TSStatic::inspectPostApply, we know that
						//we need to update the object in the db. As it turns out, nothing should need to be added, you only need
						//to update existing pos & scale vectors and rot transform, and the osmNode. Except for the (1,1,1) scale.

						//So first, let's get us a scale_id, and see if it equals one or not.
						
						scale_id_query << "SELECT sc.id FROM vector3 sc JOIN shape sh ON sh.scale_id=sc.id WHERE sh.id=" << 
							object->mOseId << ";";
						result = mSQL->ExecuteSQL(scale_id_query.str().c_str());
						scale_id_query.clear(); scale_id_query.str("");
						if (result)
						{
							resultSet = mSQL->GetResultSet(result);
							if (resultSet->iNumRows == 1) 
							{
								scaleId = dAtoi(resultSet->vRows[0]->vColumnValues[0]);
								Con::printf("static found scale id: %d",scaleId);
							}
						}

						update_query << "UPDATE vector3 SET x=" << pos.x << ",y=" << pos.y << ",z=" << pos.z << 
							" WHERE id=(SELECT pos_id FROM shape WHERE id=" << object->mOseId << ");";
						result = mSQL->ExecuteSQL(update_query.str().c_str());
						update_query.clear(); update_query.str("");

						QuatF rot(object->getTransform());
						update_query << "UPDATE rotation SET x=" << rot.x << ",y=" << rot.y << ",z=" << rot.z << ",w=" << rot.w << 
							" WHERE id=(SELECT rot_id FROM shape WHERE id=" << object->mOseId << ");";
						result = mSQL->ExecuteSQL(update_query.str().c_str());
						update_query.clear(); update_query.str("");

						Vector3 scale = object->getScale();
						//Now, make sure we don't change it if it is still (1,1,1), but also that we aren't changing it back to (1,1,1) from something else, which would be just as much time as doing this.
						Vector3 diff = scale - Vector3(1,1,1);
						if ((scaleId!=1)||(diff.len()>0.0001))
						{
							if (scaleId==1)
							{//If we used to be (1,1,1) but are changing it, we need an insert.
								insert_query << "INSERT INTO vector3 (x,y,z) VALUES (" << scale.x << "," << scale.y << "," << scale.z << ");";
								result = mSQL->ExecuteSQL(insert_query.str().c_str());
								insert_query.clear(); insert_query.str("");
								if (result)
								{
									scaleId = mSQL->getLastRowId();

									update_query << "UPDATE shape SET scale_id=" << scaleId << " WHERE id=" << object->mOseId << ";";
									result = mSQL->ExecuteSQL(update_query.str().c_str());
									update_query.clear(); update_query.str("");
								}
							} else {
								update_query << "UPDATE vector3 SET x=" << scale.x << ",y=" << scale.y << ",z=" << scale.z << 
									" WHERE id=" << scaleId << ";";
								result = mSQL->ExecuteSQL(update_query.str().c_str());
								update_query.clear(); update_query.str("");
							}
						}
						//Now, osmNode, gets a little more complicated:
						update_query << "UPDATE osmNode SET latitude=" << latLongPos.y << ",longitude=" << latLongPos.x <<
							",name='" << object->getName() << "',cell_name='" << cellName << "' " <<
							"WHERE id=(SELECT node_id FROM shape WHERE id=" << object->mOseId << ");";
						result = mSQL->ExecuteSQL(update_query.str().c_str());
						update_query.clear(); update_query.str("");

						Con::printf("Updated a static shape in %d milliseconds!!!!!!!!!!!!!!!!!!!!!!",clock()-updateStart);
					}
					continue;
				}

				//Else, oseId==0, we're inserting a new object. First check to see if we have the shapefile already.
				fileId = 0;
				file_id_query << "SELECT id FROM shapeFile WHERE path IN ('" << object->mShapeName << "');";
				Con::printf("query: %s",file_id_query.str().c_str());
				result = mSQL->ExecuteSQL(file_id_query.str().c_str());
				file_id_query.clear(); file_id_query.str("");
				if (result)
				{
					resultSet = mSQL->GetResultSet(result);
					if (resultSet->iNumRows == 1) 
						fileId = dAtoi(resultSet->vRows[0]->vColumnValues[0]);
					else if (resultSet->iNumRows > 1) 
						Con::printf("shape has been entered in the database more than once! %s",object->mShapeName);
				}
				if (fileId == 0)
				{
					insert_query << "INSERT INTO shapeFile ( path ) VALUES ('" << object->mShapeName << "');";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
					fileId = mSQL->getLastRowId();
					insert_query.clear(); insert_query.str("");
				}
				Con::printf("file id: %d",fileId);
				if (fileId==0)
					continue;//something broke, forget this shape, move on.

				if (object->getName())
					insert_query << "INSERT INTO osmNode (longitude,latitude,type,name,cell_name) " <<
						"VALUES (" << latLongPos.x << "," << latLongPos.y << ",'TSStatic','" << object->getName() << "','" << 
						cellName << "')";
				else 	insert_query << "INSERT INTO osmNode (longitude,latitude,type,cell_name) " <<
						"VALUES (" << latLongPos.x << "," << latLongPos.y << ",'TSStatic','" << cellName << "')";

				result = mSQL->ExecuteSQL(insert_query.str().c_str());
				if (result)
					nodeId = mSQL->getLastRowId();
				insert_query.clear();
				insert_query.str("");

				insert_query << "INSERT INTO vector3 (x,y,z) VALUES (" << pos.x << "," << pos.y << "," << pos.z << ");";
				result = mSQL->ExecuteSQL(insert_query.str().c_str());
				if (result)
					posId = mSQL->getLastRowId();
				insert_query.clear();
				insert_query.str("");

				QuatF rot(object->getTransform());
				insert_query << "INSERT INTO rotation (x,y,z,w) VALUES (" << rot.x << "," << rot.y << "," << rot.z <<  "," << rot.w << ");";
				result = mSQL->ExecuteSQL(insert_query.str().c_str());
				if (result)
					rotId = mSQL->getLastRowId();
				insert_query.clear();
				insert_query.str("");

				Vector3 scale = object->getScale();
				if ((scale - Vector3(1,1,1)).len() < 0.00001)
				{//SPECIAL: I may be a very bad person for doing this, but: if 99% of the time, scale is just going to be (1,1,1)
					scaleId = 1;//then I can remove a _lot_ of clutter by making an arbitrary rule - the first vector3 in the table
									//is always (1,1,1), and we don't ever change it.
				} else {
					insert_query << "INSERT INTO vector3 (x,y,z) VALUES (" << scale.x << "," << scale.y << "," << scale.z << ");";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
					insert_query.clear(); insert_query.str("");
					if (result)
						scaleId = mSQL->getLastRowId();
				}

				Con::printf("Trying to insert tsstatic, node %d, file %d, pos %d",nodeId,fileId,posId);

				if ((nodeId>0)&&(fileId>0)&&(posId>0)&&(rotId>0)&&(scaleId>0))
				{
					insert_query << "INSERT INTO shape (node_id, file_id, pos_id, rot_id, scale_id ) VALUES (" <<
						nodeId << "," << fileId << "," << posId << "," << rotId << "," << scaleId << ");";
					result = mSQL->ExecuteSQL(insert_query.str().c_str());
					shapeId = mSQL->getLastRowId();
					insert_query.clear();
					insert_query.str("");
					if (shapeId>0)
						object->mOseId = shapeId;
				}
			}
		}
	}

	unsigned int latency = clock() - startTime;
	Con::printf("saveStaticShapes() took %d milliseconds",latency);
    */
    }

    private void findStreetNodes()
    {
        /*
         
	//Okay, this is really lazy but I'm going to use the same circling out from the middle strategy here
	//as the forest uses, although both cases could probably just be a simple loop by row and column.
	Vector2 baseCellCoords = getCellCoords(mClientPos);
	Vector3 baseCell = convertLatLongToXYZ(Vector3(baseCellCoords.x,baseCellCoords.y,0.0));
	Vector3 startCell = baseCell;//This will be moving with every loop. 
	int loops = 0;
	char cellName[20];
	Vector<std::string> activeCells;

	unsigned long startTime =  clock();

	getCellName(baseCellCoords.x,baseCellCoords.y,cellName);
	activeCells.push_back(cellName);//NOW, we are just going to make a list of cellnames, and submit them all in one query.
	loops++;	

	//NOW, to loop around in ever expanding squares until we are entirely clear of streetRadius.
	float x,y;
	Vector3 iterCell;

	Con::printf("findStreetNodes, roads %d",mRoads.size());
	//Note: this is *not* the same as taking a proper vector length from baseCell to startCell - by design, because we need the whole
	//column and row to be outside of forest radius 
	while ( ((baseCell.x-startCell.x)<=mStreetRadius) || ((baseCell.y-startCell.y)<=mStreetRadius) )
	{
		startCell.x -= mCellWidth;
		startCell.y -= mCellWidth;
		iterCell = startCell;

		Vector3 cellPosLatLong;
		float closestDist;

		for (int i=0;i<(loops*2)+1;i++) // Left side, bottom to top.
		{
			if (i>0) iterCell.y += mCellWidth;
			cellPosLatLong = convertXYZToLatLong(iterCell);
			getCellName(cellPosLatLong.x,cellPosLatLong.y,cellName);
			closestDist = getForestCellClosestDist(Vector2(cellPosLatLong.x,cellPosLatLong.y),mClientPos);
			if  (closestDist<mStreetRadius)
			{
				activeCells.push_back(cellName);
			}
		}
		for (int i=1;i<(loops*2)+1;i++) // Top, left to right.
		{
			if (i>0) iterCell.x += mCellWidth;
			cellPosLatLong = convertXYZToLatLong(iterCell);
			getCellName(cellPosLatLong.x,cellPosLatLong.y,cellName);
			closestDist = getForestCellClosestDist(Vector2(cellPosLatLong.x,cellPosLatLong.y),mClientPos);
			if  (closestDist<mStreetRadius)
			{				
				activeCells.push_back(cellName);
			}
		}
		
		for (int i=1;i<(loops*2)+1;i++) // Right, top to bottom.
		{
			if (i>0) iterCell.y -= mCellWidth;
			cellPosLatLong = convertXYZToLatLong(iterCell);
			getCellName(cellPosLatLong.x,cellPosLatLong.y,cellName);
			closestDist = getForestCellClosestDist(Vector2(cellPosLatLong.x,cellPosLatLong.y),mClientPos);
			if  (closestDist<mStreetRadius)
			{
				activeCells.push_back(cellName);
			}
		}
		
		for (int i=1;i<(loops*2);i++) // Bottom, right to left.
		{
			if (i>0) iterCell.x -= mCellWidth;
			cellPosLatLong = convertXYZToLatLong(iterCell);
			getCellName(cellPosLatLong.x,cellPosLatLong.y,cellName);
			closestDist = getForestCellClosestDist(Vector2(cellPosLatLong.x,cellPosLatLong.y),mClientPos);
			if  (closestDist<mStreetRadius)
			{
				activeCells.push_back(cellName);
			}
		}
		loops++;
	}	
	
	//NOW, we have a list of cellnames, let's use them:
	// SELECT DISTINCT w.osm_id, w.name, w.type FROM osmWay w JOIN osmNode n ...JOIN osmWayNode wn ... WHERE n.cell_name IN {=' << activeCell[i]
	
	if (mSQL)
	{
		long wayId;
		int result,total_query_len=0;
		double nodeLong,nodeLat;
		std::string wayName,wayType,wayOsmId;
		std::ostringstream selectQuery,wayQuery;
		sqlite_resultset *resultSet;

		selectQuery << 
			"SELECT DISTINCT w.id, w.osm_id, w.name, w.type " << 
			"FROM osmWay w " << 
			"JOIN osmWayNode wn ON wn.way_id = w.osm_id " <<
			"JOIN osmNode n ON n.osm_id = wn.node_id " <<
			"WHERE n.cell_name IN  ( ";
		for (int i=0;i<activeCells.size();i++)
		{
			if (i<(activeCells.size()-1))
				selectQuery << "'" << activeCells[i].c_str() << "', ";
			else
				selectQuery << "'" << activeCells[i].c_str() << "' ";
		}
		selectQuery << " );";

		int queryTime = clock();
		//Con::printf("query: %s",selectQuery.str().c_str());
		result = mSQL->ExecuteSQL(selectQuery.str().c_str());
		if (result)
		{
			resultSet = mSQL->GetResultSet(result);
			selectQuery.clear();
			selectQuery.str("");
			if (resultSet->iNumRows > 0)
			{
				Con::printf("findStreetNodes found %d osmWays, query took %d milliseconds",resultSet->iNumRows,clock()-queryTime);
				for (int i=0;i<resultSet->iNumRows;i++)
				{
					int c=0;
					wayId = dAtol(resultSet->vRows[i]->vColumnValues[c++]);
					wayOsmId = resultSet->vRows[i]->vColumnValues[c++];
					wayName = resultSet->vRows[i]->vColumnValues[c++];
					wayType = resultSet->vRows[i]->vColumnValues[c++];

					osmWay kWay;
					kWay.name = wayName;
					kWay.type = wayType;

					if (mRoads.find(wayId) == mRoads.end())
					{
						mRoads[wayId].name = wayName;
						mRoads[wayId].type = wayType;
						sprintf(mRoads[wayId].osmId,"%s",wayOsmId.c_str());
						//mRoads[wayId].nodes.push_back(kNode);
						mRoads[wayId].road = NULL;
						Con::printf("pushing back a new road: id %d name %s  type %s  osm_id %s",
											wayId,wayName.c_str(),wayType.c_str(),wayOsmId.c_str());
					}// else {//Okay, this may or may not be useful in the long run. For now we're going to load the whole way
					//mRoads[wayId].nodes.push_back(kNode);//over in makeStreets, so these nodes are irrelevant. Someday, maybe.
					//}			

				}
			}
		}
	}	
	
	unsigned int latency = clock() - startTime;
	Con::printf("findStreetNodes() took %d milliseconds",latency); 
         */
    }

    float GetForestCellClosestDist(Vector2 cellPosLatLong, Vector3 pos)
    {
        Vector3[] corners = new Vector3[4];
        int closest = -1;
        float closestDist = float.MaxValue;

        corners[0] = ConvertLatLongToXYZ(new Vector3(cellPosLatLong.x, cellPosLatLong.y, 0));//SW, bottom left
        corners[1] = corners[0] + new Vector3(0, mCellWidth, 0);//NW, upper left
        corners[2] = corners[1] + new Vector3(mCellWidth, 0, 0);//NE, upper right
        corners[3] = corners[2] + new Vector3(0, -mCellWidth, 0);//SE, lower right

        for (int i = 0; i < 4; i++)
        {
            Vector3 diff = new Vector3(pos.x, pos.y, 0) - corners[i];
            if (diff.magnitude < closestDist)
            {
                closest = i;
                closestDist = diff.magnitude;
            }
        }

        return closestDist;
    }

    float GetForestCellFarthestDist(Vector2 cellPosLatLong, Vector3 pos)
    {
        Vector3 [] corners = new Vector3[4];
        int farthest = -1;
        float farthestDist = 0;

        corners[0] = ConvertLatLongToXYZ(new Vector3(cellPosLatLong.x, cellPosLatLong.y, 0));//SW, bottom left
        corners[1] = corners[0] + new Vector3(0, mCellWidth, 0);//NW, upper left
        corners[2] = corners[1] + new Vector3(mCellWidth, 0, 0);//NE, upper right
        corners[3] = corners[2] + new Vector3(0, -mCellWidth, 0);//SE, lower right

        for (int i = 0; i < 4; i++)
        {
            Vector3 diff = pos - corners[i];
            if (diff.magnitude > farthestDist)
            {
                farthest = i;
                farthestDist = diff.magnitude;
            }
        }

        return farthestDist;
    }
    //Returns collision vector with ground, raycasting from above, not including trees, roads or shapes. 
    private void getGroundAt()
    {
    }

    //Returns collision vector with ground, *including* trees, roads and shapes. (Make this and getGroundAt the same func, with arguments).
    private void getGroundAtInclusive()
    {
    }

    public GameObject GetTerrainBlock(Vector3 pos)
    {        
        Coordinates tileCoords = FindTileCoords(pos);
        for (int i = 0; i < mTerrains.Count; i++)
        {
            Coordinates diff = new Coordinates();
            diff.longitude = mTerrainCoords[i].longitude - tileCoords.longitude;
            diff.latitude = mTerrainCoords[i].latitude - tileCoords.latitude;
            if ((Math.Abs(diff.longitude) < 0.00001f) && (Math.Abs(diff.latitude) < 0.00001f))//Is this still necessary, or can we get an "equals" here?
                return mTerrains[i];
        }        
        
        return null;
    }

    private void DropTerrainBlock(int index)
    {
    }

    private void DropAllTerrains()
    {
    }

    public string getTileName(float tileStartPointLong, float tileStartPointLat)
    {
        string name = "";

        if ((tileStartPointLong < -180.0f) || (tileStartPointLong > 180.0f) || //First check  for reasonable
                        (tileStartPointLat < -90.0f) || (tileStartPointLat > 90.0f))  // coordinate bounds.
            return "";
        //Debug.Log("getTileName, tileStartPointLong " + tileStartPointLong + " tileStartPointLat " + tileStartPointLat);
        //There is probably a better way to do this, but this is for chopping the decimal down to three places. 
        double tileStartPointLongR, tileStartPointLatR;
        tileStartPointLongR = (float)((int)(tileStartPointLong * 1000.0)) / 1000.0;//(10 ^ decimalPlaces)
        tileStartPointLatR = (float)((int)(tileStartPointLat * 1000.0)) / 1000.0;//Maybe?

        char longC, latC;
        if (tileStartPointLong >= 0.0)
            longC = 'E';
        else
            longC = 'W';
        if (tileStartPointLat >= 0.0)
            latC = 'N';
        else
            latC = 'S';

        //NOW, just have to separate out the decimal part from the whole numbers part, and make sure to get
        //preceding zeroes for the decimal part, so it's always three characters.
        int majorLong, minorLong, majorLat, minorLat;//"major" for left of decimal, "minor" for right of decimal.

        majorLong = (int)tileStartPointLongR;
        majorLat = (int)tileStartPointLatR;

        minorLong = Math.Abs((int)(tileStartPointLongR * 1000.0) - (majorLong * 1000));
        minorLat = Math.Abs((int)(tileStartPointLatR * 1000.0) - (majorLat * 1000));

        //FIX!!! Need to work a negative/positive indicator into the naming convention.
        majorLong = Math.Abs(majorLong);
        majorLat = Math.Abs(majorLat);

        name += ZeroPad(2, majorLong) + "d" + ZeroPad(3, minorLong) + longC + "_" + ZeroPad(2, majorLat) + "d" + ZeroPad(3, minorLat) + latC;
             
        return name;
    }

    //Just like getTileName except four digit resolution to the right of the decimal.
    public string GetCellName(float tileStartPointLong, float tileStartPointLat)
    {
        string name = "";
        
        if ((tileStartPointLong < -180.0) || (tileStartPointLong > 180.0) || //First check  for reasonable
                        (tileStartPointLat < -90.0) || (tileStartPointLat > 90.0))  // coordinate bounds.
            return null;

        double tileStartPointLongR, tileStartPointLatR;
        tileStartPointLongR = (float)((int)(tileStartPointLong * 10000.0)) / 10000.0;//(10 ^ decimalPlaces)
        tileStartPointLatR = (float)((int)(tileStartPointLat * 10000.0)) / 10000.0;
        char longC, latC;
        if (tileStartPointLong < 0.0) longC = 'W';
        else longC = 'E';
        if (tileStartPointLat < 0.0) latC = 'S';
        else latC = 'N';

        int majorLong, minorLong, majorLat, minorLat;//"major" for left of decimal, "minor" for right of decimal.
        string majorLongStr, minorLongStr, majorLatStr, minorLatStr;

        majorLong = (int)tileStartPointLongR;
        majorLat = (int)tileStartPointLatR;

        minorLong = Math.Abs((int)(tileStartPointLongR * 10000.0) - (majorLong * 10000));
        minorLat = Math.Abs((int)(tileStartPointLatR * 10000.0) - (majorLat * 10000));

        majorLong = Math.Abs(majorLong);
        majorLat = Math.Abs(majorLat);


        name += ZeroPad(2,majorLong) + "d" + ZeroPad(4,minorLong) + longC + "_" + ZeroPad(2, majorLat) + "d" + ZeroPad(4, minorLat) + latC;
  
        return name;
    }

    private string GetCellNamePos(Vector3 position)
    {
        string name = "";
        return name;
    }

    private Vector2 GetCellCoords(Vector3 position)
    {
        Vector2 coords = new Vector2(0,0);
        return coords;
    }

    private Vector2 GetCellCoordsFromName(string cellName)
    {
        Vector2 coords = new Vector2(0, 0);
        return coords;
    }
    // End forest cells. ///////////////////////////

    private void FindClientPos()
    {
        //First, decide if we have a player or a camera, and go with one.
        if (mPlayer != null)
            mClientPos = mPlayer.transform.position;
        else if (mCamera != null)
            mClientPos = mCamera.transform.position;
        else //HERE: do a search for any active player or object with a "MainCamera" tag...? Make sure it's locally owned.
            return;
        
        mD.mClientPosLongitude = mD.mMapCenterLongitude + (mClientPos.x * mD.mDegreesPerMeterLongitude);
        mD.mClientPosLatitude  = mD.mMapCenterLatitude + (mClientPos.z * mD.mDegreesPerMeterLatitude);
        mD.mClientPosAltitude  = mClientPos.y;
    }

    private void findClientTile()
    {
        FindClientPos();

        Vector2 clientPos = new Vector2(mD.mClientPosLongitude, mD.mClientPosLatitude);
        //FIX: The following is off by one half tile width because of my (perhaps questionable) decision to put 
        //map center in the center of a tile rather than at the lower left corner of that tile. Subject to review.
        Vector2 centerTileStart = new Vector2(mD.mMapCenterLongitude - (mD.mTileWidthLongitude / 2.0f),
                                    mD.mMapCenterLatitude - (mD.mTileWidthLatitude / 2.0f));

        //mLastTileStartLong = mTileStartLongitude;
        //mLastTileStartLat = mTileStartLatitude;
        mTileStartLongitude = ((float)Math.Floor((clientPos.x - centerTileStart.x) / mD.mTileWidthLongitude) * mD.mTileWidthLongitude) + centerTileStart.x;
        mTileStartLatitude = ((float)Math.Floor((clientPos.y - centerTileStart.y) / mD.mTileWidthLatitude) * mD.mTileWidthLatitude) + centerTileStart.y;
        //Debug.Log("Tile Width Longitude: " + (mD.mTileWidthLongitude / 2.0f) + "  mapCenter " + mD.mMapCenterLongitude + " , " + mD.mMapCenterLatitude  + "  centerTileStart " + centerTileStart.x + " , " + centerTileStart.y);
    }

    public Coordinates FindTileCoords(Vector3 pos)
    {

        Coordinates latLongPos = ConvertXYZToLatLong(pos);

        //FIX: The following is off by one half tile width because of my (perhaps questionable) decision to put 
        //map center in the center of a tile rather than at the lower left corner of that tile. Subject to review.
        Vector2 mapCenter = new Vector2(mD.mMapCenterLongitude - (mD.mTileWidthLongitude / 2.0f),
                                    mD.mMapCenterLatitude - (mD.mTileWidthLatitude / 2.0f));

        double tileStartLongitude = (Math.Floor((latLongPos.longitude - mapCenter.x) / mD.mTileWidthLongitude) * mD.mTileWidthLongitude) + mapCenter.x;
        double tileStartLatitude = (Math.Floor((latLongPos.latitude - mapCenter.y) / mD.mTileWidthLatitude) * mD.mTileWidthLatitude) + mapCenter.y;

        return new Coordinates(tileStartLongitude, tileStartLatitude);    
    }

    private void loadTileGrid() //OBSOLETE I THINK
    {
        //bool loaded = false;
        //bool verbose = true;
        string tileName, heightfilename, texturefilename;//, terrainfilename;
        
        //List<loadTerrainData> loadTerrains = new List<loadTerrainData>();//This is a list of the coords and distances for each terrain 
                                                //that we need to request from the worldServer.

        float startLong = mTileStartLongitude - (mGridMidpoint * mD.mTileWidthLongitude);
        float startLat = mTileStartLatitude - (mGridMidpoint * mD.mTileWidthLatitude);
        Debug.Log("loading tile grid, client pos " + mD.mClientPosLongitude + " " + mD.mClientPosLatitude + ", client tile start " +
              mTileStartLongitude + " " + mTileStartLatitude + " local grid start " + startLong + " " + startLat);

       
        for (int y = 0; y < mGridSize; y++)
        {
            for (int x = 0; x < mGridSize; x++)
            {
                double kLong = startLong + (x * mD.mTileWidthLongitude);
                double kLat = startLat + (y * mD.mTileWidthLatitude);
                float midLong = (float)kLong + (mD.mTileWidthLongitude / 2.0f);
                float midLat = (float)kLat + (mD.mTileWidthLatitude / 2.0f);
                Vector2 tileCenterDiff = new Vector2((mD.mClientPosLongitude - midLong) * mD.mMetersPerDegreeLongitude,
                                              (mD.mClientPosLatitude - midLat) * mD.mMetersPerDegreeLatitude);
                float tileDistance = tileCenterDiff.magnitude;

                tileName = getTileName((float)kLong, (float)kLat);
                heightfilename = mD.mTerrainPath + "hght." + tileName + ".bin";// sprintf(heightfilename, "%shght.%s.bin", mD.mTerrainPath.c_str(), tileName);
                texturefilename = mD.mTerrainPath + "text." + tileName + ".bin";// sprintf(texturefilename, "%stext.%s.bin", mD.mTerrainPath.c_str(), tileName);
                //terrainfilename = mD.mTerrainPath + "terrain." + tileName + ".ter";// sprintf(terrainfilename, "%sterrain.%s.ter", mD.mTerrainPath.c_str(), tileName);

                //New way: we are guaranteeing that the center tile is loaded, so now we should try to load the other eight, or however many.
                if ((y == mGridMidpoint) && (x == mGridMidpoint))
                {
                    //Debug.Log("We are on the center tile!");
                    mTerrain = mTerrainGrid[y * mGridSize + x];
                }
                else
                {
                    if (File.Exists(heightfilename))
                    {
                        //Debug.Log("Adding terrain tile: " + heightfilename);
                        if (File.Exists(heightfilename))
                            mTerrainGrid[y * mGridSize + x] = addTerrainBlock((float)kLong, (float)kLat);
                    }
                    else
                        Debug.Log("Couldn't find terrain height bin file: " + heightfilename + " kLong " + kLong + " kLat " + kLat );


                    if (File.Exists(texturefilename))
                    {

                        //Debug.Log("Terrain layers: " + mTerrain.GetComponent<Terrain>().???);

                        
                    }

                }
            }
        }

        //loadTerrains.Clear();
    }
    /*
     * 
     * 
//First we need to clear the grid, *then* go ahead and fill it again. ???
//for (int y = 0; y < mGridSize; y++)
//{
//    for (int x = 0; x < mGridSize; x++)
//    {
//        mTerrainGrid[y * mGridSize + x] = null;
//    }
//}

//if (verbose)
//for (int c = 0; c < mTerrains.size(); c++) 
//Debug.Log("terrain " + c + " longitude " + mTerrains[c].mLongitude + " latitude " +  , , mTerrains[c]->mLongitude, mTerrains[c]->mLatitude);



    if (mTerrainGrid[y * mGridSize + x] == null) loaded = false;
    else loaded = true;

    if ((tileDistance < mD.mTileLoadRadius) && (loaded == false))
    {
        for (int c = 0; c < mTerrains.Count; c++)
        {//Could have based this off tilename comparison, but would rather do it with numbers.
            Vector3 coordPos = ConvertXYZToLatLong(mTerrains[c].transform.position); 
            if ((Math.Abs(coordPos.x - kLong) < 0.0001) && (Math.Abs(coordPos.y - kLat) < 0.0001))
            {//("<0.0001" because "==" doesn't work, floating point error is annoying.)
                loaded = true;
                mTerrainGrid[y * mGridSize + x] = mTerrains[c];
                //if (verbose)
                Debug.Log("terrain " + x + " " + y + " loaded = " + coordPos.ToString());
            }
        }
        if (loaded == false) 
        {//Here, let's check for the bin file existing first
            if (File.Exists(heightfilename))// ||
                                            //((File.Exists(terrainfilename )) && (File.Exists(texturefilename))))
            {                            
                mTerrainGrid[y * mGridSize + x] = addTerrainBlock(kLong, kLat);

            }

            //else if (mUseDataSource)//okay, now we need to make a call to worldDataSource.
            //{//HERE: I need to request ONE AT A TIME. And keep coming back here until they're all done.
            //    loadTerrainData* kData = &(loadTerrains[y * mGridSize + x]);
            //    kData->startLongitude = kLong;
            //    kData->startLatitude = kLat;
            //    kData->tileDistance = tileDistance;
            //    mLoadState = 3;//waiting for terrain.
            //    if (verbose)
            //        Debug.Log("Making call to worldDataSource, startLong " + kLong + "  startLat " + kLat);
            //}
        }
    }
    else if ((tileDistance > mD.mTileDropRadius) && (loaded == true))
    {
        //dropTerrainBlock(kLong, kLat);
        if (verbose) Debug.Log("drop this terrain block: " + x + ", " + y);
    }

    if ((x == gridMidpoint) && (y == gridMidpoint))
        mTerrain = mTerrainGrid[y * mGridSize + x];

}
}
        */

    /*
    for (int c = 0; c < mTerrains.Count; c++)
    {
        //if ((mTerrains[c].mLongitude < startLong) ||
        //    (mTerrains[c].mLongitude >= (startLong + (mGridSize * mD.mTileWidthLongitude))) ||
        //    (mTerrains[c].mLatitude < startLat) ||
        //    (mTerrains[c].mLatitude >= (startLat + (mGridSize * mD.mTileWidthLatitude))))
        //{
        //    dropTerrainBlock(c);
        //}
    }
    if (mLoadState == 2)//Meaning we didn't set it to 3, ie waiting for data.
    {
        Debug.Log("TerrainPager done loading tiles, entering checkTile loop.");
        mLoadState = 10;
    }
    else if (mLoadState == 3)//Finally, if we did set load state to three, that means we need more terrains from 
    {           //our dataSource. Which menas the current job is picking the closest one that we still need.
        float closestDist = 99999999.0f;
        int closestIndex = -1;
        loadTerrainData kData = new loadTerrainData();
        for (int i = 0; i < loadTerrains.Count; i++)
            for (int j = 0; j < loadTerrains.Count; j++)
            {
                kData = loadTerrains[j];
                if (kData.tileDistance < closestDist)
                {
                    closestDist = kData.tileDistance;
                    closestIndex = j;
                }
            }
        if (closestIndex >= 0)
        {
            kData = loadTerrains[closestIndex];
            //mDataSource.addTerrainRequest(kData.startLongitude, kData.startLatitude);//FIX: need worldDataSource now!
            mTerrainRequestTick = mCurrentTick;
        }
    }*/




    //Hmmm...
    //loadTerrains.increment();
    //loadTerrainData* kData = &(loadTerrains.last());
    //kData->startLongitude = 0.0;
    //kData->startLatitude = 0.0;
    //kData->tileDistance = FLT_MAX;

    private void checkTileGrid()
    {
        if (mD.mTileWidthLongitude == 0)
            return;//FIX: something is breaking horribly before here, getting NaN for mTileStartLongitude, etc.
        //bool verbose = false;
        string tileName, heightfilename, texturefilename;

        float startLong = mTileStartLongitude - (mGridMidpoint * mD.mTileWidthLongitude);
        float startLat = mTileStartLatitude - (mGridMidpoint * mD.mTileWidthLatitude);
        //Debug.Log("checkTileGrid -  startLong " + startLong + " startLat " + startLat + " tileStartLong " + mTileStartLongitude +
        //        " tileWidthLong " + mD.mTileWidthLongitude + " midpoint " + mGridMidpoint);
        for (int y = 0; y < mGridSize; y++)
        {
            for (int x = 0; x < mGridSize; x++)
            {
                bool loaded = false;
                float kLong = startLong + (x * mD.mTileWidthLongitude);
                float kLat = startLat + (y * mD.mTileWidthLatitude);
                float midLong = kLong + (mD.mTileWidthLongitude / 2.0f);
                float midLat = kLat + (mD.mTileWidthLatitude / 2.0f);

                //Vector2 tileCenterDiff = new Vector2((mD.mClientPosLongitude - midLong) * mD.mMetersPerDegreeLongitude,
                //                              (mD.mClientPosLatitude - midLat) * mD.mMetersPerDegreeLatitude);
                //float tileDistance = tileCenterDiff.magnitude;

                tileName = getTileName(kLong, kLat);
                heightfilename = mD.mTerrainPath + "hght." + tileName + ".bin";
                texturefilename = mD.mTerrainPath + "text." + tileName + ".bin";


                for (int c=0; c<mTerrains.Count; c++)
                {
                    if (mTerrains[c].name.Equals(tileName))
                    {
                        mTerrainGrid[y * mGridSize + x] = mTerrains[c];
                        loaded = true;
                        //Debug.Log("Terrain tile already loaded: " + tileName);
                    }
                }

                if (loaded == false)
                {
                    //Debug.Log("Terrain tile NOT loaded, adding it now: " + tileName);
                    mTerrainGrid[y * mGridSize + x] = addTerrainBlock(kLong, kLat);

                }

            }
        }
    }

    /*
    //Whoops, need a new way to do this if I create legal GameObjects at start to fill the array.
    if (mTerrainGrid[y * mGridSize + x] != null)
    {
        Terrain terr = mTerrainGrid[y * mGridSize + x].GetComponent<Terrain>();
        if (terr != null)
        {
            //if (terr.transform.position - )
        }
    }
    //if (mTerrainGrid[y * mGridSize + x].transform.position.magnitude == 0.0f) loaded = false;
    //else loaded = true;

    //Debug.Log("Checking for height file: " + heightfilename);
    if ((tileDistance < mD.mTileLoadRadius))// && (loaded == false))
    {
        Debug.Log("tile " + x + " " + y + " should be loaded, but isn't. dist " + tileDistance + ", " + kLong + " " + kLat);
        if ((File.Exists(heightfilename)) && (File.Exists(texturefilename)))
            mTerrainGrid[y * mGridSize + x] = addTerrainBlock(kLong, kLat);
        else if (mUseDataSource)
        {
            mDataSource.addTerrainRequest(kLong, kLat);
            mLoadStage = PagerLoadStage.DataRequested;//waiting for terrain.
        }
    }
    else if ((tileDistance > mD.mTileDropRadius) && (loaded == true))
    {
        dropTerrainBlock(kLong, kLat);
        //Debug.Log("tile " + x + " " + y + " should not be loaded, but is. dist " + tileDistance + ", " + kLong + " " + kLat);
    }
    else if (tileDistance > mD.mTileLoadRadius)
    {
        //Debug.Log("tile " + x + " " + y + " should not be loaded, and isn't. dist " + tileDistance + ", " + kLong + " " + kLat);
    }
    else if ((tileDistance <= mD.mTileLoadRadius) && (loaded == true))
    {
        //Debug.Log("tile " + x + " " + y + " should be loaded, and is. dist " + tileDistance + ", " + kLong + " " + kLat);
    }*/


    private GameObject addTerrainBlock(float startLong, float startLat)
    {
        //Debug.Log("Calling addTerrainBlock long " + startLong + " lat " + startLat);

        //bool terrExists = false;
        string heightfilename, texturefilename, tileName;//terrainName , terrFileName

        GameObject TerrainObj = new GameObject();
        TerrainData terrData = new TerrainData();
        
        tileName = getTileName(startLong, startLat);
        heightfilename = mD.mTerrainPath + "hght." + tileName + ".bin";
        texturefilename = mD.mTerrainPath + "text." + tileName + ".bin";
        //terrainName = "terrain." + tileName + ".ter";
        //terrFileName = mD.mTerrainPath + terrainName;
        TerrainObj.name = tileName;

        //HERE: upsert a mapNode, type = Terrain, name = tileName, lat/long saved as floats.
        int nodeId = 0;
        string idQuery = "SELECT id FROM mapNode WHERE name='" + tileName + "';";
        mDbCmd.CommandText = idQuery;
        IDataReader reader = mDbCmd.ExecuteReader();
        while (reader.Read())
        {
            try { nodeId = reader.GetInt32(0); }
            catch (IndexOutOfRangeException) { nodeId = 0; }
        }
        reader.Close();
        if (nodeId == 0)
        { 
            string insertQuery = "INSERT INTO mapNode (latitude,longitude,name,type) VALUES (" + startLat + "," + startLong +
                             ",'" + tileName + "','Terrain');";
            mDbCmd.CommandText = insertQuery;
            mDbCmd.ExecuteNonQuery();
        }
        TerrainCollider terrCollider = TerrainObj.AddComponent<TerrainCollider>();
        Terrain terr = TerrainObj.AddComponent<Terrain>();

        TerrainObj.transform.position = new Vector3(((startLong - mD.mMapCenterLongitude) * mD.mMetersPerDegreeLongitude), 0.0f,
                (startLat - mD.mMapCenterLatitude) * mD.mMetersPerDegreeLatitude);
        TerrainObj.name = tileName;

        mTerrains.Add(TerrainObj);
        mTerrainCoords.Add(new Coordinates((double)startLong,(double)(startLat)));
        //Debug.Log("Adding terrain coords: " + mTerrainCoords[mTerrainCoords.Count-1].longitude + " " + mTerrainCoords[mTerrainCoords.Count - 1].latitude);

        string assetName = "terrain." + tileName + ".asset";
        string baseAssetName = "Terrain/terrain." + tileName;//extension must be omitted! //Terrain/
        //Debug.Log("****************** LOOKING FOR TERRAIN '" + mD.mResourcePath + assetName + "':  " + File.Exists(mD.mResourcePath + assetName).ToString());
        //Debug.Log("****************** LOOKING FOR TERRAIN " + baseAssetName);
        UnityEngine.Object terrAsset = Resources.Load(baseAssetName);

        if (terrAsset != null)//(File.Exists(mD.mResourcePath + assetName))
        {
            //Debug.Log("****************** FOUND ASSET FILE!!!!");
            terrData = (TerrainData)Instantiate(terrAsset);
            int alphaRes = terrData.alphamapResolution;
            terr.terrainData = terrData;
            terr.GetComponent<TerrainCollider>().terrainData = terrData;
            int index = mTerrains.FindIndex(x => x.Equals(TerrainObj));
            float endLat = startLat + mD.mTileWidthLatitude;
            float endLong = startLong + mD.mTileWidthLongitude;
            
            //Okay, HERE: we really need a way to detect the flat terrains we are accidentally creating wherever we lack data.
            //We could go fix the original problem and delete remaining ones manually, but I kind of like them as opposed to empty space.
            //Tricky part is when we get to coastal areas, we might find tiles with mostly elevation = 0.0f, so we need to check material too.
            if (terr.SampleHeight(new Vector3(0,0,0)) == 0.0f)
            {
                Debug.Log("WE GOT A FLAT ONE, STAN!!!");
                loadTerrainData(mTerrains.Count - 1, heightfilename, texturefilename, tileName);
            }

            if (terrData.treeInstanceCount == 0)
            {
                tSplatmap = terrData.GetAlphamaps(0, 0, alphaRes, alphaRes);
                ////mRoadedTerrains.Add(index);//Hm, do we need this anymore, if we always make roads on loading each tile?
                //if (convertRoads) ConvertRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat), index);//TEMP, ROADS
                //if (makeRoads) MakeRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat),index);//TEMP, ROADS
                MakeShapes(terr);
                MakeForest(terr);

                //EDITOR ONLY
                //if (Application.isEditor)
                //    AssetDatabase.SaveAssets();//EDITOR ONLY -  ifdef?
            }
            else
            {
                tSplatmap = terrData.GetAlphamaps(0, 0, alphaRes, alphaRes);
                //mRoadedTerrains.Add(index);//Hm, do we need this anymore, if we always make roads on loading each tile?
                //if (convertRoads) ConvertRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat), index);//TEMP, ROADS
                //if (makeRoads) MakeRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat),index);//TEMP, ROADS
                MakeShapes(terr);
            }
        }
        else
        {
            CopyTerrainDataFromTo(BaseTerrain.terrainData, ref terrData);
            terr.terrainData = terrData;
            int alphaRes = terrData.alphamapResolution;

            //EDITOR ONLY
            //if (Application.isEditor)
            //    AssetDatabase.CreateAsset(terrData, "Assets/Resources/Terrain/" + assetName);//EDITOR ONLY -  ifdef?
            //Debug.Log("******************FAILED TO FIND ASSET FILE, LOADING BINS!!!!");
            loadTerrainData(mTerrains.Count - 1, heightfilename, texturefilename, tileName);
            tSplatmap = terr.terrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);

            int index = mTerrains.FindIndex(x => x.Equals(terr));
            //mRoadedTerrains.Add(index);//Hm, do we need this anymore, if we always make roads on loading each tile?
            //float endLat = mTileStartLatitude + mD.mTileWidthLatitude;
            //float endLong = mTileStartLongitude + mD.mTileWidthLongitude;
            ////MakeRoads(new Vector2(mTileStartLongitude, mTileStartLatitude), new Vector2(endLong, endLat));

            float endLat = startLat + mD.mTileWidthLatitude;
            float endLong = startLong + mD.mTileWidthLongitude;
            //if (convertRoads) ConvertRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat), index);//TEMP, ROADS
            //if (makeRoads) MakeRoads(new Vector2(startLong, startLat), new Vector2(endLong, endLat),index);//TEMP, ROADS
            MakeForest(terr);
            MakeShapes(terr);

            //EDITOR ONLY
            //if (Application.isEditor)
            //   AssetDatabase.SaveAssets();//EDITOR ONLY
        }
        return TerrainObj;
    }
    
    void MakeForest(Terrain terr)
    {
        int alphaRes = terr.terrainData.alphamapResolution;
        int detailWidth = terr.terrainData.detailResolution;
        int detailHeight = detailWidth;
        float resolutionDiffFactor = (float)alphaRes / detailWidth;
        //float[,,] splatmap =  terr.terrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);
        //tSplatmap = splatmap;
       // int textureRes = 256;
        int numPrototypes = terr.terrainData.treePrototypes.Length;

        int indexFir = 0;
        int indexOak = 3;
        int indexAspen = 1;

        int firStart = 0;
        int oakStart = 3;
        int aspenStart = 4;

        float firChance = 0.55f;
        float oakChance = 0.25f;
        float aspenChance = 0.3f;

        //for (int i = 0; i < textureRes/8; i++)
        //{
        //    for (int j = 0; j < textureRes/8; j++)
        //    {
        //        tSplatmap[j, i, indexScrub] = 0.0f;
        //        tSplatmap[j, i, indexPalm] = 0.0f;
        //        tSplatmap[j, i, indexDeciduous] = 0.0f;
        //    }
        //}
        Debug.Log("Making forests for " + terr.terrainData.name);
        TreeInstance treeInst = new TreeInstance();
        for (int i = 0; i < alphaRes; i++)
        {
            for (int j = 0; j < alphaRes; j++)
            {
                float splatFir = tSplatmap[j, i, indexFir];
                float splatOak = tSplatmap[j, i, indexOak];
                float splatAspen = tSplatmap[j, i, indexAspen];
                //float splatPalm = tSplatmap[j, i, indexPalm];
                bool madeOne = false;

                //Mixing edge zones will happen automatically, because this list 
                treeInst.position = new Vector3((float)i / (float)alphaRes + UnityEngine.Random.Range(0.0f, 1.5f / (float)alphaRes),
                                    0, (float)j / (float)alphaRes + UnityEngine.Random.Range(0.0f, 0.5f / (float)alphaRes));
                //if ((i % 32 == 0) && (j % 32 == 0))
                //    Debug.Log("Tree inst position: " + treeInst.position.ToString() + " xz " + treeInst.position.x + " " + treeInst.position.z);

                treeInst.color = Color.white;
                treeInst.lightmapColor = Color.white;
                //NOTE: this is not perfectly random, it favors the earlier types in the list if there is a gradient. But good enough for now.
                if (UnityEngine.Random.Range(0.0f, 1.0f) < (splatFir * firChance)) // 1.5 to decrease the base probability from 100% to 50%, and then reduce it farther for less than full splat.    
                {
                    treeInst.prototypeIndex = (int)UnityEngine.Random.Range((float)firStart, (float)oakStart);
                    treeInst.heightScale = 1.0f + UnityEngine.Random.Range(0.0f, 1.0f);
              
                    madeOne = true;
                }
                else if (UnityEngine.Random.Range(0.0f, 1.0f) < (splatOak * oakChance))
                {
                    //if (UnityEngine.Random.Range(0.0f, 1.0f) < 0.2)//Mix a small number of the other tree type into the deciduous area.
                    //{
                    //    treeInst.prototypeIndex = (int)UnityEngine.Random.Range((float)firStart, (float)oakStart);
                    //    treeInst.heightScale = 1.0f + UnityEngine.Random.Range(0.0f, 1.0f);
                    //}
                    //else
                    //{
                    treeInst.prototypeIndex = oakStart + (int)UnityEngine.Random.Range(0, 1.6f);
                    treeInst.heightScale = 0.5f + UnityEngine.Random.Range(0.0f, 0.5f);
                    treeInst.rotation = UnityEngine.Random.Range(0.0f, (float)Math.PI * 2.0f);
                    //}
                    madeOne = true;
                }
                else if (UnityEngine.Random.Range(0.0f, 1.0f) < (splatAspen * aspenChance))
                {
                    treeInst.prototypeIndex = aspenStart;
                    treeInst.heightScale = 0.8f + UnityEngine.Random.Range(0.0f,0.5f);
                    treeInst.rotation = UnityEngine.Random.Range(0.0f, (float)Math.PI * 2.0f);
                    madeOne = true;
                }/*
                else if (UnityEngine.Random.Range(0.0f, 1.0f) < (splatFir * firChance))
                {
                    treeInst.prototypeIndex = firStart;
                    treeInst.heightScale = 1.0f + UnityEngine.Random.Range(0.0f, 1.0f);
                    treeInst.color = Color.gray;
                    treeInst.lightmapColor = Color.gray;
                    madeOne = true;
                }*/
                if (madeOne)
                {
                    treeInst.widthScale = treeInst.heightScale;// treeInst.heightScale;
                    terr.AddTreeInstance(treeInst);
                }
            }
        }        
        terr.Flush();

    }


    /*
    Vector3 testPos = new Vector3(0.5f, 0, 0.5f);
    List<TreeInstance> newTrees = new List<TreeInstance>(terr.terrainData.treeInstances);
    var treeQuery = from TreeInstance t in newTrees 
                    where ((Math.Abs(t.position.x - testPos.x) < 0.15) && (Math.Abs(t.position.z - testPos.z) < 0.05))
                    select t;
    int c = treeQuery.Count<TreeInstance>();
    foreach (TreeInstance t in treeQuery)
        newTrees.Remove(t);
    terr.terrainData.treeInstances = newTrees.ToArray();
    terr.Flush();
    Debug.Log("Queried my tree instances, found " + c + "!!!!!");
    */


    void CopyTerrainDataFromTo(TerrainData tDataFrom, ref TerrainData tDataTo)
    {
        tDataTo.SetDetailResolution(tDataFrom.detailResolution, 8);
        tDataTo.heightmapResolution = tDataFrom.heightmapResolution;
        tDataTo.alphamapResolution = tDataFrom.alphamapResolution;
        tDataTo.baseMapResolution = tDataFrom.baseMapResolution;
        tDataTo.size = tDataFrom.size;
        tDataTo.splatPrototypes = tDataFrom.splatPrototypes;//Interesting: "splatPrototypes is obsolete - please use terrainLayers API instead."
        tDataTo.treePrototypes = tDataFrom.treePrototypes;
    }

    private void dropTerrainBlock(float startLong, float startLat)
    {        
    }

    private void reloadSkyboxImages()
    {
    }
    
    public Vector3 ConvertLatLongToXYZ(Vector3 pos)
    {
        Vector3 newPos;
        newPos.x = (pos.x - mD.mMapCenterLongitude) * mD.mMetersPerDegreeLongitude;
        newPos.y = pos.y;
        newPos.z = (pos.z - mD.mMapCenterLatitude) * mD.mMetersPerDegreeLatitude; 
        return newPos;
    }

    public Vector3 ConvertLatLongToXYZ(double longitude, float altitude, double latitude)
    {
        Vector3 newPos;
        newPos.x = (float)((longitude - (double)mD.mMapCenterLongitude) * (double)mD.mMetersPerDegreeLongitude);
        newPos.y = altitude; 
        newPos.z = (float)((latitude - (double)mD.mMapCenterLatitude) * (double)mD.mMetersPerDegreeLatitude);
        return newPos;
    }

    public Coordinates ConvertXYZToLatLong(Vector3 pos)
    {
        Coordinates newPos = new Coordinates();
        newPos.longitude = (double)(mD.mMapCenterLongitude + (pos.x * mD.mDegreesPerMeterLongitude));
        //newPos.y = pos.y;
        newPos.latitude = (double)(mD.mMapCenterLatitude + (pos.z * mD.mDegreesPerMeterLatitude));
        return newPos;
    }
    
    public Vector3 findEdgePoint(Vector3 lowLeft, Vector3 upRight, Vector3 outPos, Vector3 inPos)
    {
        Vector3 edgePos = new Vector3(0, 0, 0);
        Vector3 lowRight = new Vector3(upRight.x, 0, lowLeft.z);
        Vector3 upLeft = new Vector3(lowLeft.x, 0, upRight.z);
        //Debug.Log("Find edge point, lowLeft " + lowLeft.ToString() + " upRight " + upRight.ToString());

        //Whoops, it seems there is a little slop somewhere, it is possible to get an "outPos" that isn't really out. I think this
        //is because of overlapping 
        if ((outPos.x > lowLeft.x) && (outPos.x < upRight.x) && (outPos.z > lowLeft.z) && (outPos.z < upRight.z))
            return outPos;

        //NOW: we have a tile extending from lowLeft to upRight, we have a known inside point inPos and a known 
        //outside point outPos. We need to find the edge point edgePos. The first thing I need to know is whether
        //outPos is in a tile sharing a side with this tile, or if it's a diagonal corner tile.
        float diffProportion = 0;
        Vector3 diff = outPos - inPos;
        diff.y = 0;//Set it to sampleHeight when we get back.

        if (diff.x == 0)
        {//We are directly (precisely) north or south.
            Debug.Log("On a north/south line: x =" + inPos.x);
            if (diff.z > 0)
                edgePos = new Vector3(inPos.x, 0, upRight.z);
            else
                edgePos = new Vector3(inPos.x, 0, lowLeft.z);

            return edgePos;
        }
        else if (diff.z == 0)
        {//We are directly (precisely) to the east or west.
            Debug.Log("On an east/west line: z =" + inPos.z);
            if (diff.x > 0)
                edgePos = new Vector3(upRight.x, 0, inPos.z);
            else
                edgePos = new Vector3(lowLeft.x, 0, inPos.z);

            return edgePos;
        }

        if ((outPos.z >= lowLeft.z) && (outPos.z < upRight.z))
        {//We are in a tile to the east or west.
            if (outPos.x > upRight.x)
                diffProportion = Math.Abs((upRight.x - inPos.x) / diff.x);
            else
                diffProportion = Math.Abs((inPos.x - lowLeft.x) / diff.x);

            edgePos = inPos + (diff * diffProportion);
        }
        else if ((outPos.x >= lowLeft.x) && (outPos.x < upRight.x))
        {//We are in a tile to the north or south.
            if (outPos.z > upRight.z)
                diffProportion = Math.Abs((upRight.z - inPos.z) / diff.z);
            else
                diffProportion = Math.Abs((inPos.z - lowLeft.z) / diff.z);

            edgePos = inPos + (diff * diffProportion);
        }
        else
        {//We are on a diagonal.
            Vector3 cornerDiff = new Vector3();
            if ((outPos.x > upRight.x) && (outPos.z > upRight.z))
                cornerDiff = upRight - inPos;
            else if ((outPos.x > upRight.x) && (outPos.z < lowLeft.z))
                cornerDiff = lowRight - inPos;
            else if ((outPos.x < lowLeft.x) && (outPos.z < lowLeft.z))
                cornerDiff = lowLeft - inPos;
            else if ((outPos.x < lowLeft.x) && (outPos.z > upRight.z))
                cornerDiff = upLeft - inPos;

            double anglePos = Math.Atan(Math.Abs(diff.z / diff.x));
            double angleCorner = Math.Atan(Math.Abs(cornerDiff.z / cornerDiff.x));
            if (anglePos > angleCorner)
            { //edgpoint is on top or bottom side.
                if (outPos.z > upRight.z) //top
                    diffProportion = Math.Abs((upRight.z - inPos.z) / diff.z);
                else                      //bottom
                    diffProportion = Math.Abs((inPos.z - lowLeft.z) / diff.z);

                edgePos = inPos + (diff * diffProportion);
            }
            else
            { //edgepoint is on right or left side.
                if (outPos.x > upRight.x)  // right
                    diffProportion = Math.Abs((upRight.x - inPos.x) / diff.x);
                else                       // left
                    diffProportion = Math.Abs((inPos.x - lowLeft.x) / diff.x);
                edgePos = inPos + (diff * diffProportion);
            }
        }
        if (diffProportion > 100.0f)
            Debug.Log("Find edge point, outPos " + outPos.ToString() + "  inPos " + inPos.ToString() + " edgePos " + edgePos.ToString() + "  diffProp " + diffProportion);

        return edgePos;
    }
    /*
    private void ConvertRoads(Vector2 lowerLeft,Vector2 upperRight, int terrIndex)
    {
        if (terrIndex < 0 || terrIndex > (mTerrains.Count - 1))
            Debug.Log("Terrain index is out of range! " + terrIndex);
        else
            Debug.Log("MAKING ROADS: terrain " + mTerrains[terrIndex].name); 
        Vector3 terrLowLeft = ConvertLatLongToXYZ((double)lowerLeft.x, 0, (double)lowerLeft.y);
        Vector3 terrUpRight = ConvertLatLongToXYZ((double)upperRight.x, 0, (double)upperRight.y);

        if (mDbCmd != null)
        {
            int f_id;
            double lon, lat;
            string f_name, f_type, f_subtype, f_subsubtype;
            string selectQuery, insertQuery, updateQuery, lastIdQuery;
            string roadName;
            
            List<mapNode> nodes = new List<mapNode>();
            List<sharedFeatureNode> sharedNodes = new List<sharedFeatureNode>();
            List<mapFeature> features = new List<mapFeature>();
            List<mapFeatureConnection> featureConnections = new List<mapFeatureConnection>();
            
            sharedFeatureNode sN;
            
            selectQuery = "SELECT f.id AS f_id,f.name AS f_name,f.type as f_type," +
                "f.subtype AS f_subtype,f.subsubtype AS f_subsubtype" +
                " FROM mapNode n" +
                " JOIN mapFeatureNode fn ON fn.node_osm_id = n.osm_id" +
                " JOIN mapFeature f ON f.id = fn.feature_id" +
                " WHERE f.type='highway'" +
                " AND n.longitude>=" + lowerLeft.x + " AND n.longitude<" + upperRight.x +
                " AND n.latitude>=" + lowerLeft.y + " AND n.latitude<" + upperRight.y +
                " ORDER BY f.id;";            

            Debug.Log(selectQuery);
            mDbCmd.CommandText = selectQuery;
            IDataReader reader = mDbCmd.ExecuteReader();
            while (reader.Read())
            {
                //lon = Double.Parse(reader["longitude"].ToString());
                //lat = Double.Parse(reader["latitude"].ToString());

                //FIX FIX FIX: remove all the try/catch, instead set up all the tables to have default values 0 or "" 
                //for empty fields, and then use Parse(reader["fieldname"].ToString())
                //Otherwise we error out and fail if we try to refer to a field that doesn't have a value.
                int fid= 0, fn_id= 0, idOrd= -1, nameOrd=-1, typeOrd= -1, subTypeOrd= -1, subSubTypeOrd= -1;
                //fn_id = reader.GetOrdinal("fn_id");
                try { idOrd = reader.GetOrdinal("f_id");  }
                catch (IndexOutOfRangeException) { }                
                try   { nameOrd = reader.GetOrdinal("f_name");  }
                catch (IndexOutOfRangeException) {  }                
                try { typeOrd = reader.GetOrdinal("f_type"); }
                catch (IndexOutOfRangeException) { }
                try  {  subTypeOrd = reader.GetOrdinal("f_subtype");  }
                catch (IndexOutOfRangeException)  { }
                try { subSubTypeOrd = reader.GetOrdinal("f_subsubtype");}
                catch (IndexOutOfRangeException){  }

                fid = 0;
                if (idOrd >= 0)
                {
                    try { fid = reader.GetInt32(idOrd); }
                    catch (InvalidCastException) { }                   
                }

                f_name = "";
                if (nameOrd >= 0)
                {
                    try{ f_name = reader.GetString(nameOrd); }
                    catch (InvalidCastException) { }                    
                }

                f_type = "";
                if (typeOrd >= 0)
                {
                    try { f_type = reader.GetString(typeOrd); }
                    catch (InvalidCastException) { }
                }

                f_subtype = "";
                if (subTypeOrd >= 0)
                {
                    try { f_subtype = reader.GetString(subTypeOrd); }
                    catch (InvalidCastException) { }
                }

                f_subsubtype = "";
                if (subSubTypeOrd >= 0)
                {
                    try { f_subsubtype = reader.GetString(subSubTypeOrd); }
                    catch (InvalidCastException) { }
                }

                //nodes.Add(new mapNode {latitude = lat,longitude = lon,feature_id = f_id,feature_node_id = fn_id});
                //Debug.Log("Adding node row: " + lat + " " + lon + " " + " f id " + f_id + " type " + f_type );
                if ((!features.Exists(x => x.id == fid)))//&& (!mLoadedFeatures.Contains(f_id))
                {
                    features.Add(new mapFeature { id = fid, name = f_name, type = f_type, subtype = f_subtype, subsubtype = f_subsubtype });
                }
            }
            reader.Close();
            

            //Now, we have the list of all features that enter our tile, next step is grab all nodes from those features.
            string feature_list = "";
            foreach (mapFeature ftr in features)
            {
                feature_list += ftr.id;
                if (ftr.id != features.Last().id)
                    feature_list += ",";
            }
            //Debug.Log("Features: " + features.Count);


            //Find nodes connected to all features on our list.
            selectQuery = "SELECT latitude,longitude,n.osm_id,fn.id AS fn_id,f.id AS f_id" +
                " FROM mapNode n" +
                " JOIN mapFeatureNode fn ON fn.node_osm_id = n.osm_id" +
                " JOIN mapFeature f ON f.id = fn.feature_id" +
                " WHERE fn.feature_id IN (" + feature_list + ");";
            Debug.Log(selectQuery);
            mDbCmd.CommandText = selectQuery;
            reader = mDbCmd.ExecuteReader();
            while (reader.Read())
            {
                lon = Double.Parse(reader["longitude"].ToString());
                lat = Double.Parse(reader["latitude"].ToString());
                long n_id = Int64.Parse(reader["osm_id"].ToString());
                int fn_id = Int32.Parse(reader["fn_id"].ToString());
                f_id = Int32.Parse(reader["f_id"].ToString());
                nodes.Add(new mapNode { latitude = lat, longitude = lon, feature_id = f_id, feature_node_id = fn_id, node_id = n_id });
            }
            reader.Close();
            
            //Find nodes shared between two or more features, meaning connections.
            selectQuery = "SELECT n1.node_osm_id AS node_id, n1.feature_id AS feature_1, n2.feature_id AS feature_2, mn.longitude AS lon, mn.latitude AS lat," +
                            " f1.type AS f1_type,f2.type AS f2_type,f1.subtype AS f1_subtype,f2.subtype AS f2_subtype," +
                            " f1.subsubtype AS f1_subsubtype,f2.subsubtype AS f2_subsubtype" +
                            " FROM mapFeatureNode n1" +
                            " JOIN mapFeatureNode n2" +
                            " ON n1.node_osm_id = n2.node_osm_id " +
                            " AND n1.feature_id != n2.feature_id " +
                            " JOIN mapNode mn ON mn.osm_id = n1.node_osm_id" +
                            " JOIN mapFeature f1 ON f1.id = n1.feature_id" +
                            " JOIN mapFeature f2 ON f2.id = n2.feature_id" +
                            " WHERE mn.longitude>=" + lowerLeft.x + " AND mn.longitude<" + upperRight.x +
                            " AND mn.latitude>=" + lowerLeft.y + " AND mn.latitude<" + upperRight.y +
                            " AND f1.type='highway' AND f2.type='highway';";     
                                                        
            //Now, separate out the duplicates, and see if we have a valid X or T intersection. 
            //Then, decide which roads attach to which connection nodes, and determine the optimal Y rotation.
            mDbCmd.CommandText = selectQuery;
            reader = mDbCmd.ExecuteReader();  
            while (reader.Read())
            {
                long node_id = Int64.Parse(reader["node_id"].ToString());
                int feature_1 = Int32.Parse(reader["feature_1"].ToString());
                int feature_2 = Int32.Parse(reader["feature_2"].ToString());

                lon = Double.Parse(reader["lon"].ToString());
                lat = Double.Parse(reader["lat"].ToString());
                
                string f1_type = reader["f1_type"].ToString();
                string f2_type = reader["f2_type"].ToString();
                //string f3_type = reader["f3_type"].ToString();
                //string f4_type = reader["f4_type"].ToString();
                string f1_subtype = reader["f1_subtype"].ToString();
                string f2_subtype = reader["f2_subtype"].ToString();
                //string f3_subtype = reader["f3_subtype"].ToString();
                //string f4_subtype = reader["f4_subtype"].ToString();
                string f1_subsubtype = reader["f1_subsubtype"].ToString();
                string f2_subsubtype = reader["f2_subsubtype"].ToString();
                //string f3_subsubtype = reader["f3_subsubtype"].ToString();
                //string f4_subsubtype = reader["f4_subsubtype"].ToString();

                //TEMP: Much more to be done in terms of sorting out different road types and connection types, but first 
                //we're just going to stop right here if we're talking about paths or sidewalks rather than roads.
                if ((f1_subtype.Equals("footway")) || (f2_subtype.Equals("footway")) ||
                    (f1_subtype.Equals("track")) || (f2_subtype.Equals("track")))
                    continue;

                int index = -1;
                bool newNode = true;
                index = sharedNodes.FindIndex(x => x.node_id == node_id);
                if (index > -1)
                {
                    sN = sharedNodes[index];
                    if ((sN.feature_1 == feature_2) && (sN.feature_2 == feature_1))
                    {//Every positive match comes in twice with features reversed, so ignore these.                        
                        newNode = false;
                    }
                    else
                    {//Otherwise, we might need to add a feature_3 or 4 to an existing sharedNode, get to this later.
                        //sN.feature_3 = ..., sN.feature_4 ...
                        newNode = false;
                    }
                }
                if (newNode)
                {
                    sN = new sharedFeatureNode();
                    sN.node_id = node_id;
                    sN.feature_1 = feature_1;
                    sN.feature_2 = feature_2;
                    sN.longitude = lon;
                    sN.latitude = lat;

                    sN.f1_type = f1_type;
                    sN.f2_type = f2_type;
                    //sN.f3_type = f3_type;
                    //sN.f4_type = f4_type;
                    sN.f1_subtype = f1_subtype;
                    sN.f2_subtype = f2_subtype;
                    //sN.f3_subtype = f3_subtype;
                    //sN.f4_subtype = f4_subtype;
                    sN.f1_subsubtype = f1_subsubtype;
                    sN.f2_subsubtype = f2_subsubtype;
                    //sN.f3_subsubtype = f3_subsubtype;
                    //sN.f4_subsubtype = f4_subsubtype;

                    //TEMP: add a floating bear, so I can see where all these things ended up.
                    //Vector3 pos = ConvertLatLongToXYZ(lon, 0, lat);
                    //GameObject obj = Instantiate(mPrefabs[1]) as GameObject;
                    //pos.y = mTerrains[terrIndex].GetComponent<Terrain>().SampleHeight(pos) + 2.0f;
                    //obj.transform.position = pos;

                    sharedNodes.Add(sN);
                    //Debug.Log("sharedNode " + pos.ToString() + " node " + node_id + " feature_1 " + feature_1 + " feature_2 " + feature_2);
                }
            }
            reader.Close();


            float diffNorth, diffSouth, diffEast, diffWest;
            Vector3 dueNorth = new Vector3(0, 0, 1);
            Vector3 dueSouth = new Vector3(0, 0, -1);
            Vector3 dueEast = new Vector3(1, 0, 0);
            Vector3 dueWest = new Vector3(-1, 0, 0);

            //Now that I have my list, start making decisions about the connections.
            int nc;
            foreach (sharedFeatureNode n in sharedNodes)
            {
                Coordinates f1Last=null, f1Next = null, f2Last = null, f2Next = null;
                Vector3 f1LastPos, f1NextPos, f2LastPos, f2NextPos;
                Vector3 f1LastDiff = new Vector3(), f1NextDiff = new Vector3(), f2LastDiff = new Vector3(), f2NextDiff = new Vector3();
                mapNode f1LastNode = new mapNode(), f1NextNode = new mapNode(), f2LastNode = new mapNode(), f2NextNode = new mapNode();
                Vector3 nodePos = ConvertLatLongToXYZ(n.longitude, 0, n.latitude);
                nodePos.y = mTerrains[terrIndex].GetComponent<Terrain>().SampleHeight(nodePos);
                
                //mapNode f1LastNode, f1NextNode, f2LastNode, f2NextNode;
                var f1Nodes = from fn in nodes
                             where fn.feature_id == n.feature_1
                             orderby fn.feature_node_id
                             select fn;
                List<mapNode> f1List = f1Nodes.ToList();
                nc = 0;
                foreach (mapNode n1 in f1List)
                {
                    if (n1.latitude == n.latitude && n1.longitude == n.longitude)
                    {
                        if (nc == 0)
                        {
                            f1Next = new Coordinates { longitude = f1List[1].longitude, latitude = f1List[1].latitude };
                            f1NextNode = f1List[1];
                        }
                        else if (nc == f1List.Count - 1)
                        {
                            f1Last = new Coordinates { longitude = f1List[nc - 1].longitude, latitude = f1List[nc - 1].latitude };
                            f1LastNode = f1List[nc - 1];
                        }
                        else
                        {
                            f1Last = new Coordinates { longitude = f1List[nc - 1].longitude, latitude = f1List[nc - 1].latitude };
                            f1LastNode = f1List[nc - 1];
                            f1Next = new Coordinates { longitude = f1List[nc + 1].longitude, latitude = f1List[nc + 1].latitude };
                            f1NextNode = f1List[nc + 1];
                        }
                    }
                    nc++;
                }

                //CLEANUP: I'm sure there is a pretty way to merge this block with the above block, but we're short on time here...
                var f2Nodes = from fn in nodes
                              where fn.feature_id == n.feature_2
                              orderby fn.feature_node_id
                              select fn;
                List<mapNode> f2List = f2Nodes.ToList();
                nc = 0;
                foreach (mapNode n2 in f2List)
                {
                    if ( n2.latitude == n.latitude && n2.longitude == n.longitude)
                    {
                        if (nc == 0)
                        {
                            f2Next = new Coordinates { longitude = f2List[1].longitude, latitude = f2List[1].latitude };
                            f2NextNode = f2List[1];
                        }
                        else if (nc == f2List.Count - 1)
                        {
                            f2Last = new Coordinates { longitude = f2List[nc - 1].longitude, latitude = f2List[nc - 1].latitude };
                            f2LastNode = f2List[nc - 1];
                        }
                        else
                        {
                            f2Last = new Coordinates { longitude = f2List[nc - 1].longitude, latitude = f2List[nc - 1].latitude };
                            f2LastNode = f2List[nc - 1];
                            f2Next = new Coordinates { longitude = f2List[nc + 1].longitude, latitude = f2List[nc + 1].latitude };
                            f2NextNode = f2List[nc + 1];
                        }
                    }
                    nc++;
                }

                //These are the same in every case, so figure them out once, now.
                Vector3 nodePosXZ = nodePos;
                nodePosXZ.y = 0;
                if (f1Last != null)
                {
                    f1LastPos = ConvertLatLongToXYZ(f1Last.longitude, 0, f1Last.latitude);
                    f1LastDiff = f1LastPos - nodePosXZ;
                    f1LastDiff.Normalize();
                }
                if (f1Next != null)
                {
                    f1NextPos = ConvertLatLongToXYZ(f1Next.longitude, 0, f1Next.latitude);
                    f1NextDiff = f1NextPos - nodePosXZ;
                    f1NextDiff.Normalize();
                }
                if (f2Last != null)
                {
                    f2LastPos = ConvertLatLongToXYZ(f2Last.longitude, 0, f2Last.latitude);
                    f2LastDiff = f2LastPos - nodePosXZ;
                    f2LastDiff.Normalize();
                }
                if (f2Next != null)
                {
                    f2NextPos = ConvertLatLongToXYZ(f2Next.longitude, 0, f2Next.latitude);
                    f2NextDiff = f2NextPos - nodePosXZ;
                    f2NextDiff.Normalize();
                }
          
                //And then, start digging into the details...
                mapFeatureConnection mFC = new mapFeatureConnection();
                mFC.pos = nodePos;
                mFC.node_id = n.node_id;
                mFC.prefabName = "";

                Vector3[] dirVecs = new Vector3[4];
                
                float minAngle = 360;
                int minNS = -1, minEW = -1;
                float[] nAngles = new float[4];
                float[] eAngles = new float[2];
                bool f1T;//Feature 1 T intersection, if true then feature 1 is the main road going in and out, feature 2 starts or ends in T.
                //NOW: we get to start deciding what kind of connection we need, and which feature goes into which slot.

                /////////////////////////////// X CROSSING //////////////////////////////////////////////////////////
                if ( f1Last != null && f1Next != null && f2Last != null && f2Next != null )
                {
                    mFC.prefabName = "Default X Crossing";
                    
                    nAngles[0] = Vector3.Angle(dueNorth, f1LastDiff);
                    nAngles[1] = Vector3.Angle(dueNorth, f1NextDiff);
                    nAngles[2] = Vector3.Angle(dueNorth, f2LastDiff);
                    nAngles[3] = Vector3.Angle(dueNorth, f2NextDiff);
                    for (int i=0;i<4;i++)
                    {
                        if (nAngles[i] < minAngle)
                        {
                            minAngle = nAngles[i];
                            minNS = i;
                        }
                    }

                    //Now we know which connection node is closest to north. We *should* be able to assume the opposite side is the south vector.
                    //This could bite us, in weird cases where both roads make ninety degree turns at the intersectino, but leave that for later.
                    switch (minNS)
                    {
                        case 0:
                            mFC.node_0 = f1NextNode.node_id;
                            dirVecs[0] = f1NextDiff;
                            mFC.node_1 = f1LastNode.node_id;
                            dirVecs[1] = f1LastDiff;
                            break;
                        case 1:
                            mFC.node_0 = f1LastNode.node_id;
                            dirVecs[0] = f1LastDiff;
                            mFC.node_1 = f1NextNode.node_id;
                            dirVecs[1] = f1NextDiff;
                            break;
                        case 2:
                            mFC.node_0 = f2NextNode.node_id;
                            dirVecs[0] = f2NextDiff;
                            mFC.node_1 = f2LastNode.node_id;
                            dirVecs[1] = f2LastDiff;
                            break;
                        case 3:
                            mFC.node_0 = f2LastNode.node_id;
                            dirVecs[0] = f2LastDiff;
                            mFC.node_1 = f2NextNode.node_id;
                            dirVecs[1] = f2NextDiff;
                            break;
                    }

                    //Next step though, we need to decide for the other feature, which way is closest to east.
                    minAngle = 360;
                    if (minNS < 2) // feature 2 is east/west
                    {
                        eAngles[0] = Vector3.Angle(dueEast, f2LastDiff);
                        eAngles[1] = Vector3.Angle(dueEast, f2NextDiff);
                        for (int i = 0; i < 2; i++)
                        {
                            if (eAngles[i] < minAngle)
                            {
                                minAngle = eAngles[i];
                                minEW = i;
                            }
                        }
                        switch (minEW)
                        {
                            case 0:
                                mFC.node_2 = f2NextNode.node_id;
                                dirVecs[2] = f2NextDiff;
                                mFC.node_3 = f2LastNode.node_id;
                                dirVecs[3] = f2LastDiff;
                                break;
                            case 1:
                                mFC.node_2 = f2LastNode.node_id;
                                dirVecs[2] = f2LastDiff;
                                mFC.node_3 = f2NextNode.node_id;
                                dirVecs[3] = f2NextDiff;
                                break;
                        }
                    }
                    else // feature 1 is east/west
                    {
                        eAngles[0] = Vector3.Angle(dueEast, f1LastDiff);
                        eAngles[1] = Vector3.Angle(dueEast, f1NextDiff);
                        for (int i = 0; i < 2; i++)
                        {
                            if (eAngles[i] < minAngle)
                            {
                                minAngle = eAngles[i];
                                minEW = i;
                            }
                        }
                        switch (minEW)
                        {
                            case 0:
                                mFC.node_2 = f1NextNode.node_id;
                                dirVecs[2] = f1NextDiff;
                                mFC.node_3 = f1LastNode.node_id;
                                dirVecs[3] = f1LastDiff;
                                break;
                            case 1:
                                mFC.node_2 = f1LastNode.node_id;
                                dirVecs[2] = f1LastDiff;
                                mFC.node_3 = f1NextNode.node_id;
                                dirVecs[3] = f1NextDiff;
                                break;
                        }
                    }

                    //And, THERE. Now we know which is which, so the last remaining task is to determing an optimal rotation for this connection.
                    diffNorth = Vector3.Angle(dirVecs[1], dueNorth);
                    diffSouth = Vector3.Angle(dirVecs[0], dueSouth);
                    diffEast = Vector3.Angle(dirVecs[3], dueEast);
                    diffWest = Vector3.Angle(dirVecs[2], dueWest);
                    float angleDiff = -(diffNorth + diffSouth + diffEast + diffWest) / 4;
                    mFC.rot = angleDiff;
                    Debug.Log("X crossing, " + " " + diffNorth + " " + diffSouth + " " + diffEast + " " + diffWest + " rot " + mFC.rot);
                }
                else /////////////////////////////// T CROSSING //////////////////////////////////////////////////////////
                {
                    if (f1Last != null && f1Next != null && (f2Last != null || f2Next != null))
                    {//This means T connection, with feature 1 passing through and feature 2 starting or ending here.
                        f1T = true;//Maybe obsolete already?
                        mFC.prefabName = "Default T Crossing";

                        if (f2Last != null)
                        {
                            mFC.node_3 = f2LastNode.node_id;
                            dirVecs[3] = f2LastDiff;
                        }
                        else
                        {
                            mFC.node_3 = f2NextNode.node_id;
                            dirVecs[3] = f2NextDiff;
                        }
                        diffEast = Vector3.Angle(dirVecs[3], dueEast);
                        diffWest = Vector3.Angle(dirVecs[3], dueWest);
                        diffNorth = Vector3.Angle(dirVecs[3], dueNorth);
                        diffSouth = Vector3.Angle(dirVecs[3], dueSouth);

                        string TDir = "";//Too tired to think of a cleaner algorithm...
                        if (diffEast < 45)
                            TDir = "East";
                        else if (diffWest < 45)
                            TDir = "West";
                        else if (diffNorth < 45)
                            TDir = "North";
                        else if (diffSouth < 45)
                            TDir = "South";
                        minAngle = 360.0f;

                        Debug.Log(TDir + "   angle " + diffEast + " " + dirVecs[3].ToString() + " " + dueEast.ToString());

                        if (TDir.Equals("East"))
                        {//We get to use it as is, with 0 = south and 1 = north.
                            nAngles[0] = Vector3.Angle(dueNorth, f1LastDiff);
                            nAngles[1] = Vector3.Angle(dueNorth, f1NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f1NextNode.node_id;
                                dirVecs[0] = f1NextDiff;
                                mFC.node_1 = f1LastNode.node_id;
                                dirVecs[1] = f1LastDiff;
                            }
                            else
                            {
                                mFC.node_0 = f1LastNode.node_id;
                                dirVecs[0] = f1LastDiff;
                                mFC.node_1 = f1NextNode.node_id;
                                dirVecs[1] = f1NextDiff;
                            }
                            diffNorth = Vector3.Angle(dirVecs[1], dueNorth);
                            diffSouth = Vector3.Angle(dirVecs[0], dueSouth);
                            mFC.rot = -(diffNorth + diffSouth + diffEast) / 3;
                            Debug.Log("T crossing, " + " " + diffNorth + " " + diffSouth + " " + diffEast + " rot " + mFC.rot);
                        }
                        else if (TDir.Equals("West"))
                        {//We need to flip it by 180, plus or minus adjustments, but 1 = south and 0 = north.
                            nAngles[0] = Vector3.Angle(dueNorth, f1LastDiff);
                            nAngles[1] = Vector3.Angle(dueNorth, f1NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f1LastNode.node_id;
                                dirVecs[0] = f1LastDiff;
                                mFC.node_1 = f1NextNode.node_id;
                                dirVecs[1] = f1NextDiff;
                            }
                            else
                            {
                                mFC.node_0 = f1NextNode.node_id;
                                dirVecs[0] = f1NextDiff;
                                mFC.node_1 = f1LastNode.node_id;
                                dirVecs[1] = f1LastDiff;
                            }
                            diffSouth = Vector3.Angle(dirVecs[1], dueSouth);
                            diffNorth = Vector3.Angle(dirVecs[0], dueNorth);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffNorth + diffSouth + diffWest) / 3 + 180.0f;
                            //mFC.rot = -(diffNorth + diffSouth + diffEast) / 3 + 180.0f;
                            Debug.Log("T crossing flipped, " + " " + diffNorth + " " + diffSouth + " " + diffWest + " rot " + mFC.rot);
                        }
                        else if (TDir.Equals("North"))
                        {
                            nAngles[0] = Vector3.Angle(dueWest, f1LastDiff);
                            nAngles[1] = Vector3.Angle(dueWest, f1NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f1NextNode.node_id;
                                dirVecs[0] = f1NextDiff;
                                mFC.node_1 = f1LastNode.node_id;
                                dirVecs[1] = f1LastDiff;
                            }
                            else
                            {
                                mFC.node_0 = f1LastNode.node_id;
                                dirVecs[0] = f1LastDiff;
                                mFC.node_1 = f1NextNode.node_id;
                                dirVecs[2] = f1NextDiff;
                            }
                            diffWest = Vector3.Angle(dirVecs[1], dueWest);
                            diffEast = Vector3.Angle(dirVecs[0], dueEast);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffWest + diffEast + diffNorth) / 3 + 270.0f;
                        }
                        else if (TDir.Equals("South"))
                        {
                            nAngles[0] = Vector3.Angle(dueWest, f1LastDiff);
                            nAngles[1] = Vector3.Angle(dueWest, f1NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f1LastNode.node_id;
                                dirVecs[0] = f1LastDiff;
                                mFC.node_1 = f1NextNode.node_id;
                                dirVecs[1] = f1NextDiff;
                            }
                            else
                            {
                                mFC.node_0 = f1NextNode.node_id;
                                dirVecs[0] = f1NextDiff;
                                mFC.node_1 = f1LastNode.node_id;
                                dirVecs[1] = f1LastDiff;
                            }
                            diffEast = Vector3.Angle(dirVecs[1], dueEast);
                            diffWest = Vector3.Angle(dirVecs[0], dueWest);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffWest + diffEast + diffSouth) / 3 + 90.0f;
                        }
                    }
                    else if (f2Last != null && f2Next != null && (f1Last != null || f1Next != null))
                    {//This means T connection, with feature 2 passing through and feature 1 starting or ending here.                        
                        f1T = false;
                        mFC.prefabName = "Default T Crossing";
                                                
                        if (f1Last != null)
                        {
                            mFC.node_3 = f1LastNode.node_id;
                            dirVecs[3] = f1LastDiff;
                        }
                        else
                        {
                            mFC.node_3 = f1NextNode.node_id;
                            dirVecs[3] = f1NextDiff;
                        }
                        diffEast = Vector3.Angle(dirVecs[3], dueEast);
                        diffWest = Vector3.Angle(dirVecs[3], dueWest);
                        diffNorth = Vector3.Angle(dirVecs[3], dueNorth);
                        diffSouth = Vector3.Angle(dirVecs[3], dueSouth);

                        string TDir = "";//Too tired to think of a cleaner algorithm...
                        if (diffEast < 45)
                            TDir = "East";
                        else if (diffWest < 45)
                            TDir = "West";
                        else if (diffNorth < 45)
                            TDir = "North";
                        else if (diffSouth < 45)
                            TDir = "South";
                        minAngle = 360.0f;

                        if (TDir.Equals("East"))
                        {//We get to use it as is, with 0 = south and 1 = north.
                            nAngles[0] = Vector3.Angle(dueNorth, f2LastDiff);
                            nAngles[1] = Vector3.Angle(dueNorth, f2NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f2NextNode.node_id;
                                dirVecs[0] = f2NextDiff;
                                mFC.node_1 = f2LastNode.node_id;
                                dirVecs[1] = f2LastDiff;
                            }
                            else
                            {
                                mFC.node_0 = f2LastNode.node_id;
                                dirVecs[0] = f2LastDiff;
                                mFC.node_1 = f2NextNode.node_id;
                                dirVecs[1] = f2NextDiff;
                            }
                            diffNorth = Vector3.Angle(dirVecs[1], dueNorth);
                            diffSouth = Vector3.Angle(dirVecs[0], dueSouth);
                            mFC.rot = -(diffNorth + diffSouth + diffEast) / 3;
                            Debug.Log("T crossing, " + " " + diffNorth + " " + diffSouth + " " + diffEast + " rot " + mFC.rot);
                        }
                        else if (TDir.Equals("West"))
                        {//We need to flip it by 180, plus or minus adjustments, but 1 = south and 0 = north.
                            nAngles[0] = Vector3.Angle(dueNorth, f2LastDiff);
                            nAngles[1] = Vector3.Angle(dueNorth, f2NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f2LastNode.node_id;
                                dirVecs[0] = f2LastDiff;
                                mFC.node_1 = f2NextNode.node_id;
                                dirVecs[1] = f2NextDiff;
                            }
                            else
                            {
                                mFC.node_0 = f2NextNode.node_id;
                                dirVecs[0] = f2NextDiff;
                                mFC.node_1 = f2LastNode.node_id;
                                dirVecs[1] = f2LastDiff;
                            }
                            diffSouth = Vector3.Angle(dirVecs[1], dueSouth);
                            diffNorth = Vector3.Angle(dirVecs[0], dueNorth);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffNorth + diffSouth + diffWest) / 3 + 180.0f;
                            //mFC.rot = -(diffNorth + diffSouth + diffEast) / 3 + 180.0f;
                            Debug.Log("T crossing flipped, " + " " + diffNorth + " " + diffSouth + " " + diffWest + " rot " + mFC.rot);
                        }
                        else if (TDir.Equals("North"))
                        {
                            nAngles[0] = Vector3.Angle(dueWest, f2LastDiff);
                            nAngles[1] = Vector3.Angle(dueWest, f2NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f2NextNode.node_id;
                                dirVecs[0] = f2NextDiff;
                                mFC.node_1 = f2LastNode.node_id;
                                dirVecs[1] = f2LastDiff;
                            }
                            else
                            {
                                mFC.node_0 = f2LastNode.node_id;
                                dirVecs[0] = f2LastDiff;
                                mFC.node_1 = f2NextNode.node_id;
                                dirVecs[2] = f2NextDiff;
                            }
                            diffWest = Vector3.Angle(dirVecs[1], dueWest);
                            diffEast = Vector3.Angle(dirVecs[0], dueEast);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffWest + diffEast + diffNorth) / 3 + 270.0f;
                        }
                        else if (TDir.Equals("South"))
                        {
                            nAngles[0] = Vector3.Angle(dueWest, f2LastDiff);
                            nAngles[1] = Vector3.Angle(dueWest, f2NextDiff);
                            if (nAngles[0] < nAngles[1])
                            {
                                mFC.node_0 = f2LastNode.node_id;
                                dirVecs[0] = f2LastDiff;
                                mFC.node_1 = f2NextNode.node_id;
                                dirVecs[1] = f2NextDiff;
                            }
                            else
                            {
                                mFC.node_0 = f2NextNode.node_id;
                                dirVecs[0] = f2NextDiff;
                                mFC.node_1 = f2LastNode.node_id;
                                dirVecs[1] = f2LastDiff;
                            }
                            diffEast = Vector3.Angle(dirVecs[1], dueEast);
                            diffWest = Vector3.Angle(dirVecs[0], dueWest);
                            //diffWest = Vector3.Angle(f2NextDiff, dueWest);
                            mFC.rot = -(diffWest + diffEast + diffSouth) / 3 + 90.0f;
                        }
                        Debug.Log("T crossing flipped, " + " " + diffNorth + " " + diffSouth + " " + diffEast + " rot " + mFC.rot);
                        
                    }
                    else
                    {
                        //If neither, then we have a bad connection, leave prefabName at "" and use that to bail on the process.
                        //But maybe do something more interesting here later.
                    }
                }
                if (mFC.prefabName.Length > 0)
                {
                    featureConnections.Add(mFC);
                }
            }
            
            
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            foreach (mapFeature ftr in features)
            {
                //Debug.Log("Highway: " + ftr.id + "  " + ftr.name + " type " + ftr.type + " subtype " + ftr.subtype);
                if (ftr.name.Length > 0)
                    roadName = ftr.name;
                else
                    roadName = "road_" + ftr.id;

                var featureNodes = from fn in nodes
                                   where fn.feature_id == ftr.id
                                   orderby fn.feature_node_id
                                   select fn;
                List<mapNode> nodeList = featureNodes.ToList();

                Terrain terrainTile = null;
                GameObject terrainObj = null;
                List<Vector3> markerList = new List<Vector3>();
                Vector3 lastPos = new Vector3(0, 0, 0);
                int alphaRes = 0;
                bool onTerrain = false;
                bool onTerrainLast = false;
                bool endRoadSegment = false;
                mapFeatureConnection startFC, endFC;
                ERConnection startConn = null, endConn = null, sourceConn = null;
                int startConnType = 0, endConnType = 0;// 1 = X crossing, 2 = T crossing, from database ids
                int startNode = 0;
                bool hasStartConn = false, hasEndConn = false;

                int c = 0;
                for (int k = 0; k < nodeList.Count; k++)
                {
                    mapNode mN = nodeList[k];

                    Vector3 pos = ConvertLatLongToXYZ(mN.longitude, 0, mN.latitude);
                    terrainObj = GetTerrainBlock(pos);
                    
                    if (terrainObj == null)
                        onTerrain = false;
                    else
                    {
                        terrainTile = terrainObj.GetComponent<Terrain>();
                        pos.y = terrainTile.SampleHeight(pos);
                        alphaRes = terrainTile.terrainData.alphamapResolution;
                        if (terrainObj != mTerrains[terrIndex])
                            onTerrain = false;
                        else
                            onTerrain = true;
                    }


                    if (c == 0)
                    {
                        startFC = featureConnections.Find(x => x.node_id == mN.node_id);
                        if (startFC.node_id > 0)
                        {
                            startNode = k;
                            hasStartConn = true;
                            //    Debug.Log("Feature connection: " + startFC.node_id + " rot " + startFC.rot);
                            //    string connName = "Connection_" + startFC.node_id;
                            startConnType = 1;
                            if (startFC.prefabName.Equals("Default T Crossing"))
                                startConnType = 2;
                        //startConn = mRoadNetwork.GetConnectionByName(connName);
                        //if (startConn == null)
                        //    startConn = mRoadNetwork.InstantiateConnection(sourceConn, connName, pos, new Vector3(0, startFC.rot, 0));
                        }
                    }
                    else
                    {
                        endFC = featureConnections.Find(x => x.node_id == mN.node_id);
                        //string connName = "Connection_" + endFC.node_id;
                        if (endFC.node_id > 0)
                        {
                            hasEndConn = true;
                            //    endConn = mRoadNetwork.GetConnectionByName(connName);
                            endConnType = 1;
                            if (endFC.prefabName.Equals("Default T Crossing"))
                                endConnType = 2;

                            //if (endConn == null)
                            //    endConn = mRoadNetwork.InstantiateConnection(sourceConn, connName, pos, new Vector3(0, endFC.rot, 0));
                            endRoadSegment = true;
                        }
                    }


                    //Now, sort through the possibilities.
                    if (!onTerrain && !onTerrainLast)
                    {//Not on terrain and wasn't on last time either.
                        onTerrainLast = onTerrain;
                        lastPos = pos;
                        continue;
                    }
                    else if (onTerrain && !onTerrainLast && c > 0)
                    {//Stepping on to the terrain tile.
                        Vector3 edgePos = findEdgePoint(terrLowLeft, terrUpRight, lastPos, pos);
                        if (edgePos.magnitude > 0)
                        {
                            edgePos.y = terrainTile.SampleHeight(edgePos);// + 0.25f;                                
                            markerList.Add(edgePos);
                        }
                        markerList.Add(pos);
                    }
                    else if (!onTerrain && onTerrainLast)
                    {//Stepping off of the terrain tile.
                        Vector3 edgePos = findEdgePoint(terrLowLeft, terrUpRight, pos, lastPos);
                        if (edgePos.magnitude > 0)
                        {
                            edgePos.y = terrainTile.SampleHeight(edgePos);// + 0.25f;  //Can't seem to make RaiseOffset work, or make this hack work.                              
                            markerList.Add(edgePos);
                        }

                        onTerrainLast = onTerrain;
                        lastPos = pos;
                        endRoadSegment = true;
                        //continue;
                    }
                    else if (onTerrain && (onTerrainLast || c == 0))
                    {//On terrain last time and still is.
                        markerList.Add(pos);
                    }

                    if (k == nodeList.Count - 1)
                        endRoadSegment = true;

                    ///////////////////////////////////////////////////////////////////////
                    if (endRoadSegment)
                    {  //Now, we need to make a road segment - either we've run out of nodes or we've run into a connection.
                        int type_id, road_id, start_id = 0, end_id = 0;

                        //TEMP: figure out a better way to link these up.
                        if (ftr.subtype.Equals("track") || ftr.subtype.Equals("service"))
                            type_id = 3;
                        else if (ftr.subtype.Equals("footway"))
                            type_id = 5;
                        else if (ftr.subtype.Equals("cycleway"))
                            type_id = 4;
                        else if (ftr.subtype.Equals("motorway"))
                            type_id = 2;
                        else
                            type_id = 1;

                        //HERE: check to see if it exists first! But this requires more information, feature id isn't enough.
                        insertQuery = "INSERT INTO erRoad (feature_id,road_type_id) VALUES (" + ftr.id + "," + type_id + ");";
                        mDbCmd.CommandText = insertQuery;
                        mDbCmd.ExecuteNonQuery();

                        lastIdQuery = "SELECT last_insert_rowid();";
                        mDbCmd.CommandText = lastIdQuery;
                        reader = mDbCmd.ExecuteReader();
                        reader.Read();
                        road_id = reader.GetInt32(0);
                        reader.Close();

                        for (int d = 0; d < markerList.Count; d++)
                        {
                            Vector3 p = markerList[d];                            
                            insertQuery = "INSERT INTO erRoadMarker (road_id,pos_x,pos_y,pos_z) VALUES (" + road_id + "," + p.x + "," + p.y + "," + p.z + ");";
                            mDbCmd.CommandText = insertQuery;
                            mDbCmd.ExecuteNonQuery();

                            if ((d == 0) || (d == markerList.Count - 1))//save ids for the beginning and end nodes.
                            {
                                mDbCmd.CommandText = lastIdQuery;
                                reader = mDbCmd.ExecuteReader();
                                reader.Read();
                                if (d == 0)
                                    start_id = reader.GetInt32(0);
                                else
                                    end_id = reader.GetInt32(0);
                                reader.Close();
                            }
                        }
                        
                        int connIndex = 0;
                        if (hasStartConn)
                        {
                            mapNode kNode = nodeList[startNode + 1];
                            startFC = featureConnections.Find(x => x.node_id == nodeList[startNode].node_id);
                            if (kNode.node_id == startFC.node_0)
                                connIndex = 0;
                            else if (kNode.node_id == startFC.node_1)
                                connIndex = 1;
                            else if (kNode.node_id == startFC.node_2)
                                connIndex = 2;
                            else if (kNode.node_id == startFC.node_3)
                                connIndex = 3;
                            //Debug.Log("Attaching startconn: " + connIndex);



                            int conn_id = 0;
                            selectQuery = "SELECT id FROM erConnection WHERE pos_x=" + startFC.pos.x + " AND pos_y=" + startFC.pos.y +
                                " AND pos_z=" + startFC.pos.z + ";";
                            mDbCmd.CommandText = selectQuery;
                            reader = mDbCmd.ExecuteReader();
                            if (reader.Read())
                                conn_id = reader.GetInt32(0);
                            reader.Close();

                            if (conn_id > 0)
                            {
                                updateQuery = "UPDATE erConnection SET node_" + connIndex + "=" + start_id + " WHERE id=" + conn_id + ";";
                                mDbCmd.CommandText = updateQuery;
                                mDbCmd.ExecuteNonQuery();
                            }
                            else
                            {
                                insertQuery = "INSERT INTO erConnection (connection_type_id,pos_x,pos_y,pos_z,rot_y,node_" + connIndex + ")" +
                                    " VALUES (" + startConnType + "," + startFC.pos.x + "," + startFC.pos.y + "," + startFC.pos.z + "," +
                                    startFC.rot + "," + start_id + ");";
                                mDbCmd.CommandText = insertQuery;
                                mDbCmd.ExecuteNonQuery();
                            }
                            
                            //try { mRoad.ConnectToStart(startConn, connIndex); }
                            //catch
                            //{
                            //    Debug.Log("WHOOPS! failed to attach start connection,  " + roadName + " node " + nodeList[startNode].node_id);
                            //}
                        }
                        if (hasEndConn)
                        {
                            mapNode kNode = nodeList[k - 1];
                            endFC = featureConnections.Find(x => x.node_id == mN.node_id);
                            if (kNode.node_id == endFC.node_0)
                                connIndex = 0;
                            else if (kNode.node_id == endFC.node_1)
                                connIndex = 1;
                            else if (kNode.node_id == endFC.node_2)
                                connIndex = 2;
                            else if (kNode.node_id == endFC.node_3)
                                connIndex = 3;
                            //Debug.Log("Attaching endconn: " + connIndex);

                            int conn_id = 0;
                            selectQuery = "SELECT id FROM erConnection WHERE pos_x=" + endFC.pos.x + " AND pos_y=" + endFC.pos.y +
                                " AND pos_z=" + endFC.pos.z + ";";
                            mDbCmd.CommandText = selectQuery;
                            reader = mDbCmd.ExecuteReader();
                            if (reader.Read())
                                conn_id = reader.GetInt32(0);
                            reader.Close();
                            if (conn_id > 0)
                            {
                                updateQuery = "UPDATE erConnection SET node_" + connIndex + "=" + end_id + " WHERE id=" + conn_id + ";";
                            }
                            else
                            {
                                insertQuery = "INSERT INTO erConnection (connection_type_id,pos_x,pos_y,pos_z,rot_y,node_" + connIndex + ")" +
                                " VALUES (" + endConnType + "," + endFC.pos.x + "," + endFC.pos.y + "," + endFC.pos.z + "," +
                                endFC.rot + "," + end_id + ");";
                                mDbCmd.CommandText = insertQuery;
                                mDbCmd.ExecuteNonQuery();
                            }

                            //try { mRoad.ConnectToEnd(endConn, connIndex); }
                            //catch
                            //{
                            //    Debug.Log("WHOOPS! failed to attach end connection,  " + roadName + " node " + mN.node_id);
                            //}
                        }

                        endRoadSegment = false;
                        hasStartConn = false;
                        hasEndConn = false;
                        markerList.Clear();
                        c = -1;//Needs to go back to zero, but it's going to get incremented after this.

                        //HERE: We need to back up one, so that we can start our next road segment from THIS mapNode, not the next one.
                        if (k != nodeList.Count - 1)
                            k--;

                    }

                    onTerrainLast = onTerrain;
                    lastPos = pos;

                    c++;
                }
                reader.Dispose();
            }
        }
    }


    //OKAY: next move. We need to read from the erRoad, erRoadMarker, erConnection tables, and make the road network from there.
    //Using the following lists to store the ids, so that we can save changes back to the database when we're done editing.
    //List<int> mRoadIds;
    //List<List<int>> mRoadMarkerIds;
    //List<int> mConnectionIds;
    private void MakeRoads(Vector2 lowerLeft, Vector2 upperRight, int terrIndex)
    {
        if (terrIndex < 0 || terrIndex > (mTerrains.Count - 1))
            Debug.Log("Terrain index is out of range! " + terrIndex);
        else
            Debug.Log("MAKING ROADS: terrain " + mTerrains[terrIndex].name + " lowLeft " + lowerLeft.ToString() + " upRight " + upperRight.ToString());

        Vector3 terrLowLeft = ConvertLatLongToXYZ((double)lowerLeft.x, 0, (double)lowerLeft.y);
        Vector3 terrUpRight = ConvertLatLongToXYZ((double)upperRight.x, 0, (double)upperRight.y);
        

        ERRoadType roadType;
        ERConnection sourceConn;

        if (mDbCmd != null)
        {
            string selectQuery, insertQuery, updateQuery, lastIdQuery;
            string roadName;
            IDataReader reader;

            List<erRoad> roads = new List<erRoad>();
            List<erRoadMarker> roadMarkers = new List<erRoadMarker>();

            selectQuery = "SELECT m.id as m_id,r.id as r_id,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z," +
                    " control_type,r.road_type_id,f.name " +
                    " FROM erRoadMarker m" +
                    " JOIN erRoad r ON r.id = m.road_id" +
                    " JOIN mapFeature f ON f.id = r.feature_id" +
                    " WHERE m.pos_x>=" + terrLowLeft.x + " AND m.pos_x<=" + terrUpRight.x +
                    " AND m.pos_z>=" + terrLowLeft.z + " AND m.pos_z<=" + terrUpRight.z +
                    " ORDER BY r.id;";

            //Debug.Log(selectQuery);
            mDbCmd.CommandText = selectQuery;
            reader = mDbCmd.ExecuteReader();
            while (reader.Read())
            {
                float x, y, z, rx, ry, rz;
                int id, rid, control_type, road_type;
                string road_name;
                id = Int32.Parse(reader["m_id"].ToString());
                rid = Int32.Parse(reader["r_id"].ToString());
                x = float.Parse(reader["pos_x"].ToString());
                y = float.Parse(reader["pos_y"].ToString());
                z = float.Parse(reader["pos_z"].ToString());
                rx = float.Parse(reader["rot_x"].ToString());
                ry = float.Parse(reader["rot_y"].ToString());
                rz = float.Parse(reader["rot_z"].ToString());
                control_type = Int32.Parse(reader["control_type"].ToString());
                road_type = Int32.Parse(reader["road_type_id"].ToString());
                road_name = reader["name"].ToString();
                Vector3 rPos = new Vector3(x, y, z);
                Vector3 rRot = new Vector3(rx, ry, rz);

                erRoadMarker rm = new erRoadMarker();
                rm.id = id;
                rm.road_id = rid;
                rm.control_type = control_type;
                rm.pos = rPos;
                rm.rot = rRot;
                roadMarkers.Add(rm);

                
                if (mRoadIds.Contains(rid))//if we have already dealt with this road in another tile, then we must have 
                    continue; //grabbed a single edge marker, in which case disregard it.                               

                int road_index = roads.FindIndex(r => r.id == rid);
                if (road_index < 0)
                {//HERE: need to make sure we have more than one marker from a road, before adding it to our mRoadIds list.
                    //To do that, let's wait until we get a second node before adding a road to the list.
                    List<erRoadMarker> tm = roadMarkers.FindAll(m => m.road_id == rid);
                    if (tm.Count > 1)
                    {
                        erRoad er = new erRoad();
                        er.id = rid;
                        er.name = road_name;
                        er.type_id = road_type;
                        roads.Add(er);
                        //Debug.Log("Adding road: " + rid);
                    }
                }

                //Debug.Log("Road " + road_name + " marker: " + rPos.ToString() + " type " + road_type);
            }
            reader.Close();

            //List<int> mRoadIds;
            //Dictionary<int,List<int>> mRoadMarkerIds;
            //List<int> mConnectionIds;

            //Okay so now I have a list of markers, and of roads, so we can search through those without hitting the DB again.
            foreach (erRoad er in roads)
            {
                var thisRoadMarkers = from rm in roadMarkers
                                      where rm.road_id == er.id
                                      orderby rm.id
                                      select rm;
                List<erRoadMarker> markerList = thisRoadMarkers.ToList();

                mRoadIds.Add(er.id);
                mRoadMarkerIds.Add(er.id, new List<int>());

                Vector3[] markers = new Vector3[markerList.Count];
                for (int d = 0; d < markerList.Count; d++)
                {
                    markers[d] = markerList[d].pos;//TEMP do this on the next pass, now. Load from DB, create road.                    
                    mRoadMarkerIds[er.id].Add(markerList[d].id);
                }

                //FIX - with above setup, make general purpose from database.
                roadType = null;
                if (er.type_id == 1)
                    roadType = roadTypeHwy;
                else if (er.type_id == 2)
                    roadType = roadTypeMotorway;
                else if (er.type_id == 3)
                    roadType = roadTypeDirt;
                else if (er.type_id == 4)
                    roadType = roadTypeSidewalk;
                else if (er.type_id == 5)
                    roadType = roadTypePath;

                //Debug.Log("Creating road! " + er.name + " " + er.id + " type " + er.type_id);
                mRoad = mRoadNetwork.CreateRoad(er.name, roadType, markers);
                //if (mRoad != null)
                //    Debug.Log("Created road! " + er.id + " " + er.name);
                //else
                //    Debug.Log("Failed to create road! " + er.id + " " + er.name);
            }

            
            selectQuery = "SELECT id,connection_type_id,node_0,node_1,node_2,node_3," +
                " pos_x,pos_y,pos_z,rot_x,rot_y,rot_z" +
                " FROM erConnection" +
                " WHERE pos_x >= " + terrLowLeft.x + " AND pos_x<" + terrUpRight.x +
                " AND pos_z>=" + terrLowLeft.z + " AND pos_z<" + terrUpRight.z + ";";
            Debug.Log(selectQuery);
            mDbCmd.CommandText = selectQuery;
            reader = mDbCmd.ExecuteReader();
            while (reader.Read())
            {
                int id, connection_type, node_0, node_1, node_2, node_3;
                float x, y, z, rx, ry, rz;
                id = Int32.Parse(reader["id"].ToString());
                connection_type = Int32.Parse(reader["connection_type_id"].ToString());
                node_0 = Int32.Parse(reader["node_0"].ToString());
                node_1 = Int32.Parse(reader["node_1"].ToString());
                node_2 = Int32.Parse(reader["node_2"].ToString());
                node_3 = Int32.Parse(reader["node_3"].ToString());
                x = float.Parse(reader["pos_x"].ToString());
                y = float.Parse(reader["pos_y"].ToString());
                z = float.Parse(reader["pos_z"].ToString());
                rx = float.Parse(reader["rot_x"].ToString());
                ry = float.Parse(reader["rot_y"].ToString());
                rz = float.Parse(reader["rot_z"].ToString());

                if (connection_type == 1)
                    sourceConn = X_SourceConn;
                else if (connection_type == 2)
                    sourceConn = T_SourceConn;
                else
                    continue;

                Debug.Log("Making connection " + id + ": " + x + " " + y + " " + z + " rot " + ry);
                ERConnection tConn = mRoadNetwork.InstantiateConnection(sourceConn, "Connection_" + id, new Vector3(x, y, z), new Vector3(0, ry, 0));
                if (tConn == null)
                    return;
                mConnectionIds.Add(id);

                
                //HERE: connect the roads!!
                if (node_0 > 0)
                {
                    int roadId = roadMarkers.Find(rm => rm.id == node_0).road_id;
                    int roadIndex = mRoadIds.FindIndex(r => r == roadId);
                    ERRoad kRoad = mRoadNetwork.GetRoads()[roadIndex];
                    Debug.Log("roads length: " + roads.Count + " roadNetwork: " + mRoadNetwork.GetRoads().Length + " roadIndex " + roadIndex);
                    int markerIndex = mRoadMarkerIds[roadId].FindIndex(rm => rm == node_0);
                    try
                    {
                        if (markerIndex == 0)
                            kRoad.ConnectToStart(tConn, 0);
                        else
                            kRoad.ConnectToEnd(tConn, 0);
                    }
                    catch { }
                }
                if (node_1 > 0)
                {
                    int roadId = roadMarkers.Find(rm => rm.id == node_1).road_id;
                    int roadIndex = mRoadIds.FindIndex(r => r == roadId);
                    ERRoad kRoad = mRoadNetwork.GetRoads()[roadIndex];
                    int markerIndex = mRoadMarkerIds[roadId].FindIndex(rm => rm == node_1);
                    try
                    {
                        if (markerIndex == 0)
                            kRoad.ConnectToStart(tConn, 1);
                        else
                            kRoad.ConnectToEnd(tConn, 1);
                    }
                    catch { }
                }
                if (node_2 > 0)
                {
                    int roadId = roadMarkers.Find(rm => rm.id == node_2).road_id;
                    int roadIndex = mRoadIds.FindIndex(r => r == roadId);
                    ERRoad kRoad = mRoadNetwork.GetRoads()[roadIndex];
                    int markerIndex = mRoadMarkerIds[roadId].FindIndex(rm => rm == node_2);
                    try
                    {
                        if (markerIndex == 0)
                            kRoad.ConnectToStart(tConn, 2);
                        else
                            kRoad.ConnectToEnd(tConn, 2);
                    }
                    catch { }
                }
                if (node_3 > 0)
                {
                    int roadId = roadMarkers.Find(rm => rm.id == node_3).road_id;
                    int roadIndex = mRoadIds.FindIndex(r => r == roadId);
                    ERRoad kRoad = mRoadNetwork.GetRoads()[roadIndex];
                    int markerIndex = mRoadMarkerIds[roadId].FindIndex(rm => rm == node_3);
                    try
                    {
                        if (markerIndex == 0)
                            kRoad.ConnectToStart(tConn, 3);
                        else
                            kRoad.ConnectToEnd(tConn, 3);
                    }
                    catch { }
                }
                
            }
            reader.Close();
            

            
             
            //mRoad.GetMarkerPosition(int marker);
            //mRoad.GetMarkerTilting(int marker);
            //ERConnection tConn = mRoad.GetConnectionObjectAtEnd();
            //mRoad.SnapToTerrain(true, 1.0f);
            //for (int f = 0; f < markerList.Count; f++)
            //{
            //    mRoad.SetMarkerControlType(f, ERMarkerControlType.StraightXZ);
            //}

        
            //mRoadNetwork.BuildRoadNetwork();//What does this do exactly?


             
        }
    }
    */
    /*
                if (node_0 > 0)
                {
                    erRoadMarker rm = roadMarkers.Find(m => m.id == node_0);
                    int rIndex = mRoadIds.FindIndex(m => m == rm.road_id);
                    ERRoad road = mRoadNetwork.GetRoads()[rIndex];
                    int mIndex = mRoadMarkerIds[rIndex].FindIndex(m => m == rm.id);
                    if (mIndex == 0) // try/catch?
                        road.ConnectToStart(tConn, 0);
                    else
                        road.ConnectToEnd(tConn, 0);
                }

                if (node_1 > 0)
                {
                    erRoadMarker rm = roadMarkers.Find(m => m.id == node_1);
                    int rIndex = mRoadIds.FindIndex(m => m == rm.road_id);
                    ERRoad road = mRoadNetwork.GetRoads()[rIndex];
                    int mIndex = mRoadMarkerIds[rIndex].FindIndex(m => m == rm.id);
                    if (mIndex == 0)
                        road.ConnectToStart(tConn, 1);
                    else
                        road.ConnectToEnd(tConn, 1);
                }

                if (node_2 > 0)
                {
                    erRoadMarker rm = roadMarkers.Find(m => m.id == node_2);
                    int rIndex = mRoadIds.FindIndex(m => m == rm.road_id);
                    ERRoad road = mRoadNetwork.GetRoads()[rIndex];
                    int mIndex = mRoadMarkerIds[rIndex].FindIndex(m => m == rm.id);
                    if (mIndex == 0)
                        road.ConnectToStart(tConn, 2);
                    else
                        road.ConnectToEnd(tConn, 2);
                }
                if (node_3 > 0)
                {
                    erRoadMarker rm = roadMarkers.Find(m => m.id == node_3);
                    int rIndex = mRoadIds.FindIndex(m => m == rm.road_id);
                    ERRoad road = mRoadNetwork.GetRoads()[rIndex];
                    int mIndex = mRoadMarkerIds[rIndex].FindIndex(m => m == rm.id);
                    if (mIndex == 0)
                        road.ConnectToStart(tConn, 3);
                    else
                        road.ConnectToEnd(tConn, 3);
                }           */



    /*
     

        
                        /////////////////////////////////////////////////////////////////////////////////////////////////////
                        //NOW: we need to do our best to clear the forest away from the roads. To do this we step along the 
                        //road between known markers, and paint a no-tree material onto the tsplatmap, a certain radius back.
                        //FIX: can we move this to an external function maybe? Do it for the whole feature in one pass?
                        for (int e = 1; e < markers.Length; e++)
                        {
                            int splatRadius = 3;
                            int indexFir = 0;
                            int indexOak = 3;
                            int indexAspen = 1;

                            Vector3 mpos = markers[e];
                            lastPos = markers[e - 1];
                            //Debug.Log( " terrStart " + terrLowLeft.ToString() + " terrEnd " + terrUpRight.ToString() + " markerPos " + pos.ToString());
                            if ((mpos.x > terrLowLeft.x) && (mpos.x < terrUpRight.x) && (mpos.z > terrLowLeft.z) && (mpos.z < terrUpRight.z) && (alphaRes > 0))
                            {
                                if (lastPos.magnitude > 0)
                                {
                                    Vector3 diff = lastPos - mpos;//Vector from pos to lastPos.
                                    int steps = (int)(diff.magnitude / (mD.mTileWidth / alphaRes));
                                    if (steps == 0)
                                        steps = 1;
                                    Vector3 diffStep = diff * (1 / steps);
                                    for (int s = 0; s < steps; s++)
                                    {
                                        Vector3 stepPos = mpos + (diffStep * s);
                                        Vector2 relPos = new Vector2((stepPos.x - terrLowLeft.x), (stepPos.z - terrLowLeft.z));
                                        Vector2 splatPos = new Vector2((int)((relPos.x / mD.mTileWidth) * alphaRes), (int)((relPos.y / mD.mTileWidth) * alphaRes));
                                        //Debug.Log("lastPos " + lastPos.ToString() + "  relPos: " + relPos.x + " " + relPos.y + "  splatPos " + splatPos.x + " " + splatPos.y);

                                        for (int i = (int)splatPos.x - splatRadius; i < splatPos.x + splatRadius; i++)
                                        {
                                            for (int j = (int)splatPos.y - splatRadius; j < splatPos.y + splatRadius; j++)
                                            {
                                                if ((i >= 0) && (i < alphaRes) && (j >= 0) && (j < alphaRes))
                                                {
                                                    tSplatmap[j, i, indexFir] = 0.0f;
                                                    tSplatmap[j, i, indexOak] = 0.0f;
                                                    tSplatmap[j, i, indexAspen] = 0.0f;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }



             * 
             * 
            //TEMP, MOVE /////////////
            if (ftr.subtype.Equals("track") || ftr.subtype.Equals("service"))
                mRoad = mRoadNetwork.CreateRoad(roadName, roadTypeDirt, markers);
            else if (ftr.subtype.Equals("footway"))
                mRoad = mRoadNetwork.CreateRoad(roadName, roadTypePath, markers);
            else if (ftr.subtype.Equals("cycleway"))
                mRoad = mRoadNetwork.CreateRoad(roadName, roadTypeSidewalk, markers);
            else if (ftr.subtype.Equals("motorway"))
                mRoad = mRoadNetwork.CreateRoad(roadName, roadTypeMotorway, markers);
            else
                mRoad = mRoadNetwork.CreateRoad(roadName, roadTypeHwy, markers);
     */

        /*
    public void SaveRoads()
    {
        Debug.Log("Saving road network! roads " + mRoadNetwork.GetRoads().Length);

        ERRoad[] roads = mRoadNetwork.GetRoads();
        ERConnection[] connections = mRoadNetwork.GetConnections();
        int rc = 0, cc = 0;
        foreach (ERRoad r in roads)
        {
            int roadId = mRoadIds[rc];
            Debug.Log("Road id: " + roadId);
            rc++;
        }
        foreach (ERConnection c in connections)
        {

            int connId = mConnectionIds[cc];
            Debug.Log("Connection id: " + connId);
            cc++;
        }



    }*/

    private void loadTerrainData(int index, string heightFile,string textureFile,string tileName)
    {
        FileStream fs = new FileStream(heightFile, FileMode.Open, FileAccess.Read);
        float fileLong, fileLat, tileWidth;
        int heightmapRes = 0,textureRes = 0;

        Terrain terr = mTerrains[index].GetComponent<Terrain>();
        TerrainData tData = terr.terrainData;
        //Debug.Log("loadTerrainData, Heightfile: " + heightFile + "   terrain position " + terr.transform.position.ToString());// + " terrData layers " + tData.terrainLayers.Length);
        int heightRes = tData.heightmapResolution;
        float terrHeight = tData.size.y;
        Debug.Log("Height Map Resolution: " + heightRes);
        int c = 0;
        int heightBinArgs = 5;
        float value = 0.0f;
        byte [] b = new byte[sizeof(float)];
        for (int i=0; i < heightBinArgs; i++ )
        {
            fs.Read(b, 0, b.Length);
            value = BitConverter.ToSingle(b, 0);
            //Debug.Log("value: " + value);
            if (i == 0)
                fileLong = value;
            else if (i == 1)
                fileLat = value;
            else if (i == 2)
                tileWidth = value;
            else if (i == 3)
                heightmapRes = (int)value;
            else if (i == 4)
                textureRes = (int)value;
        }
        if (heightmapRes == 0)
        {
            Debug.Log("Height Map Resolution is zero! Bailing.");
            return;
        }

        bool done = false;
        float minElev = float.MaxValue;
        float maxElev = -float.MaxValue;
        float deltaElev = 0.0f;
        float[,] Tiles = new float[heightRes, heightRes];//For SetHeights, we need an array of values from 0.0 to 1.0
        float[,] realElev = new float[heightRes, heightRes];//But the bin files are stored as actual heights, in meters.
        while (done == false)
        {
            for (int i = 0; i < heightRes; i++)//horizontal
            {
                for (int j = 0; j < heightRes; j++)//vertical
                {
                    if (fs.Read(b, 0, b.Length) <= 0)
                        done = true;
                    else
                    {
                        value = BitConverter.ToSingle(b, 0);
                        realElev[i, j] = value;// (inputValues[count] * mDeltaElev) + mMinElev;
                        if (value < minElev)
                            minElev = value;
                        if (value > maxElev)
                            maxElev = value;
                        //if ((i % 32 == 0) && (j % 32 == 0))
                        //    Debug.Log("height value " + i + " " + j + " = " + value);
                        //TEMP TEMP TEMP
                        //if (j == (heightmapRes - 1))//Last row, TEMP TEMP let's make an extra repeated value till we fix the source files
                        //    realElev[i, j + 1] = value;
                    }
                }
            }
        }
        fs.Close();

        deltaElev = maxElev - minElev;//Currently unused, might want later.
        
        //TEMP - WHOOPS -  our height bin files are actually one less resolution: 256 instead of 257. OOPS, fix that on FG side.
        //for (int i = 0; i < heightRes; i++)
        //{
        //    for (int j = 0; j < heightRes; j++)
        //    {
        //        Tiles[i, j] = 0.0f;
        //    }
        //}

        for (int i = 0; i < heightRes; i++)//horizontal
        {
            for (int j = 0; j < heightRes; j++)//vertical
            {
                Tiles[i, j] = realElev[i, j] / terrHeight;//Ah, right, this is a value from 0 to terrain height, which I need to find. 
                                                       //For now, I know it's 1000 meters. TEMP TEMP TEMP                                                       

            }
        }
        
        tData.SetHeights(0, 0, Tiles);//Now, did we get it going in the right direction?
        tData.name = tileName;
        string assetName = "Assets/Resources/Terrain/" + tileName + ".asset";
        //AssetDatabase.CreateAsset(tData, assetName);


        ////////////// TEXTURES //////////////////////////////////////////
        //(This all seems like it might benefit from splitting off into another function.)

        SplatPrototype[] splatPrototypes = tData.splatPrototypes;
        int alphaRes = tData.alphamapResolution;
        
        textureRes = 256;//TEMP
        //int tex_array_size = (textureRes * textureRes) * 2;//Okay, now we have to move to shorts instead of chars, because we have numbers up to 584, not 256.
        if (!File.Exists(textureFile))
            return;

        FileStream tfs = new FileStream(textureFile, FileMode.Open, FileAccess.Read);
        //BinaryReader tbr = new BinaryReader(tfs);
        //byte[] tex_bytes_received = new byte[tex_array_size];
        //short[,] texValues = new short[textureRes, textureRes];
        //tex_bytes_received = tbr.ReadBytes(tex_array_size);
        //tbr.Close();

        //Here, we might want to add a string layer to match up forest to forest, etc.
        //For now this will get it done though.
        IDictionary<int, int> texDict = new Dictionary<int, int>();
        /*
        texDict[0] = 6;//gravel
        texDict[1] = 3;//city
        texDict[2] = 0;//evergreen
        texDict[3] = 5;//grass
        texDict[4] = 1;//sand?
        texDict[5] = 2;//asphalt?
        texDict[6] = 4;//forest
        texDict[7] = 2;//asphalt
        */

        //FIX FIX FIX FIX

        texDict[0] = 6;
        texDict[1] = 2;
        texDict[2] = 2;

        texDict[10] = 0;
        texDict[11] = 0;
        texDict[13] = 0;
        texDict[14] = 0;
        texDict[18] = 0;

        texDict[20] = 0;
        texDict[22] = 0;
        texDict[26] = 0;

        texDict[32] = 0;

        texDict[43] = 0;
        texDict[44] = 0;
        texDict[45] = 0;
        texDict[47] = 0;
        texDict[48] = 0;

        texDict[52] = 0;
        texDict[54] = 0;
        texDict[55] = 0;
        texDict[56] = 0;
        texDict[57] = 0;
        texDict[58] = 0;
        texDict[59] = 0;

        texDict[65] = 0;
        texDict[66] = 0;
        texDict[67] = 0;
        texDict[68] = 0;
        texDict[69] = 0;

        texDict[70] = 0;
        texDict[71] = 0;
        texDict[72] = 0;
        
        texDict[100] = 0;

        texDict[128] = 0;

        texDict[135] = 0;
        texDict[136] = 0;

        texDict[141] = 0;

        texDict[156] = 0;

        texDict[162] = 0;
        texDict[166] = 0;

        texDict[170] = 0;//forest
        texDict[171] = 0;//forest
        texDict[172] = 0;

        texDict[185] = 3;

        texDict[194] = 3;

        texDict[204] = 3;

        texDict[220] = 3;
        texDict[222] = 3;

        texDict[244] = 3;
        texDict[245] = 3;
        texDict[246] = 3;

        texDict[250] = 0;

        texDict[274] = 0;
        texDict[276] = 0;
        texDict[278] = 0;

        texDict[301] = 1;
        texDict[321] = 1;

        texDict[441] = 1;

        texDict[514] = 2;

        texDict[555] = 7;//agriculture
        texDict[556] = 7;//agriculture
        texDict[557] = 7;//agriculture

        texDict[567] = 1;
        texDict[568] = 3;//clearcut
        texDict[569] = 3;//clearcut

        texDict[573] = 1;
        texDict[579] = 6;//water

        texDict[581] = 5;//city
        texDict[582] = 5;//city
        texDict[583] = 5;//city
        texDict[584] = 5;
        

        //Debug.Log(  " textureRes: " + textureRes + " alphamap res " + alphaRes + "  tex_array_size " + tex_array_size + " array length: " + tex_bytes_received.Length);
        //for (int n=0;n< splatPrototypes.Length;n++)
        //{
        //    Debug.Log("Splat " + n + " texture: " + splatPrototypes[n].texture.ToString() + " tileSize " + splatPrototypes[n].tileSize +
        //        " tile offset " + splatPrototypes[n].tileOffset);
        //}

        byte[] sb = new byte[sizeof(short)];
        float[,,] splatmapData = tData.GetAlphamaps(0, 0, alphaRes, alphaRes);
        short val;
        for (int i = 0; i < textureRes; i++)
        {
            for (int j = 0; j < textureRes; j++)
            {
                //int count = (((j+(y*mTextureRes))*tex_area_X_int) + (i+(x*mTextureRes)));
                int count = ((j * textureRes) + i);
                
                //Debug.Log("Trying to index array, " + i + " , " + j + " count " + count);

                tfs.Read(sb, 0, sb.Length);
                val = BitConverter.ToInt16(sb, 0);
                //t = tex_bytes_received[count];
                if (mAllLandCovers.IndexOf(val) <= 0)
                    mAllLandCovers.Add(val);

                //if ((j == 0) && (i < 24))
                //    Debug.Log("row value: " + val);

                if (texDict.ContainsKey(val))
                {
                    for (int k = 0; k < splatPrototypes.Length; k++)
                    {

                        if (texDict[val] == k)
                            splatmapData[j, i, k] = 1.0f;
                        else
                            splatmapData[j, i, k] = 0.0f;

                        
                        //if ((i < 24 0) && (j == 0))
                        //    Debug.Log("texture index: " +  (int)t);
                    }
                }
                else
                {
                    //Debug.Log("Key not found: " + t);
                    if (mUnfoundLandCovers.IndexOf(val) <= 0)
                        mUnfoundLandCovers.Add(val);
                }
            }
        }
        tData.SetAlphamaps(0, 0, splatmapData);
        //Debug.Log("UNFOUND LAND COVERS:");
        for (int i=0;i< mUnfoundLandCovers.Count;i++)
        {
            Debug.Log(" unfound: " + mUnfoundLandCovers[i]);
        }
        tfs.Close();
        //Debug.Log("*************************************************************** Textured tile: " + tileName);
    }

    public string ZeroPad(int digits, int value)
    {
        if (value < 0)
        {
            Debug.Log("Error: padZero only works on positive integers.");
            return null;
        }
        string str = "";
        switch (digits)
        {
            case 2:
                if (value < 10) str = "0";
                break;
            case 3:
                if (value < 10) str = "00";
                else if (value < 100) str = "0";
                break;
            case 4:
                if (value < 10) str = "000";
                else if (value < 100) str = "00";
                else if (value < 1000) str = "0";
                break;
            case 5:
                if (value < 10) str = "0000";
                else if (value < 100) str = "000";
                else if (value < 1000) str = "00";
                else if (value < 10000) str = "0";
                break;
            case 6:
                if (value < 10) str = "00000";
                else if (value < 100) str = "0000";
                else if (value < 1000) str = "000";
                else if (value < 10000) str = "00";
                else if (value < 100000) str = "0";
                break;
        }
        str += value.ToString();

        return str;
    }


    //THIS IS ONLY A TEST
    void loadTestDat()
    {
        int args = 5;
        string filename = Application.dataPath + "/TerrainMaster/Terrain/testTile.bin";
        FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        float fileLong = 0, fileLat = 0, tileWidth = 0;
        int heightmapRes = 0, textureRes = 0;

        int c = 0;
        int heightBinArgs = 5;
        float value = 0.0f;
        byte[] b = new byte[sizeof(float)];
        for (int i = 0; i < heightBinArgs; i++)
        {
            fs.Read(b, 0, b.Length);
            value = BitConverter.ToSingle(b, 0);
            //Debug.Log("value: " + value);
            if (i == 0)
                fileLong = value;
            else if (i == 1)
                fileLat = value;
            else if (i == 2)
                tileWidth = value;
            else if (i == 3)
                heightmapRes = (int)value;
            else if (i == 4)
                textureRes = (int)value;
        }
        Debug.Log("****************************** Loading testTile.bin, fileLong " + fileLong + " tileWidth " + tileWidth + " textureRes " + textureRes);
    }


    void setupRoadTyes()
    {
        erRoadType rP = new erRoadType();



        //roadTypeHwy
        rP.id = 1;//(Later this will come from DB)
        rP.roadWidth = 5.0f;
        rP.roadMaterial = "Materials/roads/road material";
        rP.layer = 1;
        rP.tag = "Road";
        //rP.sidewalks = false;
        //rP.streetlights = false;
        //rP.guardrails = false;
        

    }

    public void RunTerrainPager()
    {
        mEditorMode = true;

        Start();

        checkTileGrid();

        Debug.Log("Started terrain pager!!! degreesPerMeterLong " + mD.mDegreesPerMeterLongitude);

    }

    public void StopTerrainPager()
    {

    }
    
    /*
    public void MakeARoad()
    {

        mRoadNetwork = new ERRoadNetwork();
        //ERRoad mRoad;
        //ERConnection mConn = new ERConnection();
        
        //mRoad.ConnectToStart()
        //mRoad.AttachToStart();
        
        GameObject roadNetObj = GameObject.Find("Road Network");
        ERModularBase modBase = roadNetObj.GetComponent<ERModularBase>();        
        
        //(-10,142,330) is a good spot for one segment in a road that I can see from the starting position.
        foreach (ERRoadType rt in mRoadNetwork.GetRoadTypes())
        {
            Debug.Log("Road type width: " + rt.roadWidth + "   " + rt.roadTypeName);
        }
        ERRoadType rT = mRoadNetwork.GetRoadTypes()[0];

        Vector3[] markers = new Vector3[3];
        markers[0] = new Vector3(-10.0f, 142.9f, 330.0f);
        markers[1] = new Vector3(-15.0f, 142.9f, 300.0f);
        markers[2] = new Vector3(20.0f, 142.9f, 300.0f);
        ERRoad myRoad = mRoadNetwork.CreateRoad("my Awesome Road", rT, markers);
        myRoad.SnapToTerrain(true);

        Vector3[] markers2 = new Vector3[4];
        markers2[0] = new Vector3(20.0f, 142.9f, 330.0f);
        markers2[1] = new Vector3(20.0f, 142.9f, 300.0f);
        markers2[2] = new Vector3(20.0f, 142.9f, 270.0f);
        markers2[3] = new Vector3(20.0f, 142.9f, 220.0f);
        ERRoad myOtherRoad = mRoadNetwork.CreateRoad("my Cross Road", rT, markers2);
        myOtherRoad.SnapToTerrain(true);

        ERRoad myLatestRoad;
        ERConnection mConn = mRoadNetwork.GetSourceConnectionByName("Default X Crossing");
        //ERConnection iConn = mRoadNetwork.GetSourceConnectionByName("Default I Crossing");
        if (mConn != null)
        {
            ERConnection tempConn;// = new ERConnection(;
            myLatestRoad = myOtherRoad.InsertIConnector(1,"tempConnector", out tempConn);
            tempConn.Destroy();

            ERConnection nConn = mRoadNetwork.InstantiateConnection(mConn, "newConnection", markers2[1], new Vector3(0, 0, 0));
            int index = nConn.FindNearestConnectionIndex(markers[1]);
            myOtherRoad.ConnectToEnd(nConn,1);
            myLatestRoad.ConnectToStart(nConn, 0);
            myRoad.ConnectToEnd(nConn, 2);
            Debug.Log("Made a crossing! " + mConn.name + " winning index: " + index);
        }

        else Debug.Log("Failed to make a crossing.");
        //GameObject myCrossing = Instantiate()
        //ERCrossingPrefabs myPrefab = modBase.defaultTCrossing
        //roadNet.AddIntersection
        //roadNet.AddIntersection(myPrefab, myCrossing);
        // Assets/EasyRoads3D/prefab meshes/Default X Crossing_dynamic.asset

    
        //roadNet.ConnectRoads(myRoad, 0, myOtherRoad, 2);
    }
    */


}






























/* 
 * //Possible order of texture keys:
    mTerrainMaterials.push_back( "TT_Gravel_02" );//
    mTerrainMaterials.push_back( "TT_Mud_07" );
    mTerrainMaterials.push_back( "TT_Grass_20" );
    mTerrainMaterials.push_back( "TT_Earth_01" );//TT_Snow_01
    mTerrainMaterials.push_back( "TT_Sand_01" );
    mTerrainMaterials.push_back( "TT_Rock_14" );
    mTerrainMaterials.push_back( "forest1a_mat" );
    mTerrainMaterials.push_back( "TT_Grass_01" );

 */


/*
//Hm, need to come up with the best C# way to replicate the large block ostringstream syntax below.
//Looks like "+" is going to be my best bet, from initial research.
selectQuery = "SELECT id,feature_id " + //,latitude,longitude
                "FROM mapNode WHERE latitude>" + mTileStartLatitude + " AND latitude<" + endLat +
                " AND longitude>" + mTileStartLongitude + " AND longitude<" + endLong + ";";


Debug.Log(selectQuery);

//selectQuery = "SELECT id,osm_id,name FROM osmNode WHERE id<10;";
//selectQuery = "SELECT id,node_id FROM shape;";
mDbCmd.CommandText = selectQuery;
IDataReader reader = mDbCmd.ExecuteReader();
int id, feature_id;
double lat, lon;
List<int> highways = new List<int>();
string strFeatures = "";
while (reader.Read())
{
    id = Int32.Parse(reader["id"].ToString()); //reader.GetInt32(0);// reader.GetInt32(0);
    if (reader["feature_id"].ToString().Length > 0)
    {
        feature_id = Int32.Parse(reader["feature_id"].ToString());
        //features.Add(feature_id);
        strFeatures += feature_id + ",";
    }
    else feature_id = 0;
    //Debug.Log("id = " + id  + ",  feature_id = " + feature_id );
}
reader.Close();

string cleanFeatures = strFeatures.Remove(strFeatures.Length - 1);//Remove final comma.
selectQuery = "SELECT id FROM mapFeature WHERE type='highway' AND id IN (" + cleanFeatures + ");";
mDbCmd.CommandText = selectQuery;
reader = mDbCmd.ExecuteReader();
while (reader.Read())
{
    id = Int32.Parse(reader["id"].ToString());
    highways.Add(id);
}
reader.Close();
for (int i = 0; i < highways.Count; i++)
{
    //FIX FIX FIX: excessive DB access, we already got all this data above, or could have, should store it 
    //there and then loop back through it instead of asking again.
    selectQuery = "SELECT id,latitude,longitude FROM mapNode WHERE feature_id=" + highways[i] + ";";
    mDbCmd.CommandText = selectQuery;
    reader = mDbCmd.ExecuteReader();
    List<Vector3> markerVectors = new List<Vector3>();
    while (reader.Read())
    {
        id = Int32.Parse(reader["id"].ToString()); //reader.GetInt32(0);// reader.GetInt32(0);
        lat = Double.Parse(reader["latitude"].ToString());
        lon = Double.Parse(reader["longitude"].ToString());
        Vector3 pos = ConvertLatLongToXYZ(new Vector3((float)lon, 0, (float)lat));
        pos.y = terr.SampleHeight(pos);
        markerVectors.Add(pos);
        Debug.Log("adding a road marker: " + pos.ToString() + " coords " + lon + " " + lat);
    }
    reader.Close();

    Vector3[] markers = new Vector3[markerVectors.Count];
    //Hm, is this really the only way to convert from a List to an Array?
    for (int j=0;j<markerVectors.Count;j++) 
        markers[j] = markerVectors[j];
    string roadName = "road " + i;
    mRoad = mRoadNetwork.CreateRoad(roadName, roadType, markers);
}*/




/*
private void loadStaticShapes()
{

   Vector2 baseCellCoords = GetCellCoords(mClientPos);
   Vector3 baseCell = ConvertLatLongToXYZ(new Vector3(baseCellCoords.x, baseCellCoords.y, 0));

   Vector3 startCell = baseCell;//This will be moving with every loop.
   int loops = 0;
   string cellName;
   List<string> activeCells = new List<string>();

   //unsigned long startTime =  clock(); //
   cellName = GetCellName(mTileStartLongitude, mTileStartLatitude);
   Debug.Log("loadStaticShapes!!! cellname " + cellName + " baseCellCoords "  + baseCellCoords.ToString() + " clientPos " + mClientPos.ToString());
   cellName = "123d0069W_43d9261N";
   activeCells.Add(cellName);
   loops++;

   //NOW, our strategy is to loop around in ever expanding squares until we reach shapeRadius.
   Vector3 iterCell;
   while (((baseCell.x - startCell.x) <= mShapeRadius) || ((baseCell.y - startCell.y) <= mShapeRadius))
   {
       startCell.x -= mCellWidth;
       startCell.y -= mCellWidth;
       iterCell = startCell;

       Vector3 cellPosLatLong;
       float closestDist;

       for (int i = 0; i < (loops * 2) + 1; i++) // Left side, bottom to top.
       {
           if (i > 0) iterCell.y += mCellWidth;
           cellPosLatLong = ConvertXYZToLatLong(iterCell);
           cellName = GetCellName(cellPosLatLong.x, cellPosLatLong.y);
           closestDist = GetForestCellClosestDist(new Vector2(cellPosLatLong.x, cellPosLatLong.y), mClientPos);
           if (closestDist < mForestRadius)
           {
               activeCells.Add(cellName);
           }
       }
       for (int i = 1; i < (loops * 2) + 1; i++) // Top, left to right.
       {
           if (i > 0) iterCell.x += mCellWidth;
           cellPosLatLong = ConvertXYZToLatLong(iterCell);
           cellName = GetCellName(cellPosLatLong.x, cellPosLatLong.y);
           closestDist = GetForestCellClosestDist(new Vector2(cellPosLatLong.x, cellPosLatLong.y), mClientPos);
           if (closestDist < mForestRadius)
           {
               activeCells.Add(cellName);
           }
       }
       for (int i = 1; i < (loops * 2) + 1; i++) // Right, top to bottom.
       {
           if (i > 0) iterCell.y -= mCellWidth;
           cellPosLatLong = ConvertXYZToLatLong(iterCell);
           cellName = GetCellName(cellPosLatLong.x, cellPosLatLong.y);
           closestDist = GetForestCellClosestDist(new Vector2(cellPosLatLong.x, cellPosLatLong.y), mClientPos);
           if (closestDist < mForestRadius)
           {
               activeCells.Add(cellName);
           }
       }
       for (int i = 1; i < (loops * 2); i++) // Bottom, right to left.
       {
           if (i > 0) iterCell.x -= mCellWidth;
           cellPosLatLong = ConvertXYZToLatLong(iterCell);
           cellName = GetCellName(cellPosLatLong.x, cellPosLatLong.y);
           closestDist = GetForestCellClosestDist(new Vector2(cellPosLatLong.x, cellPosLatLong.y), mClientPos);
           if (closestDist < mForestRadius)
           {
               activeCells.Add(cellName);
           }
       }
       loops++;
   }
   if (mDbCmd != null)
   {
       long nodeId, fileId, posId, rotId, scaleId, shapeId;
       int result, total_query_len = 0;
       double nodeLong, nodeLat;
       string shapeFile, name, selectQuery;

       //Hm, need to come up with the best C# way to replicate the large block ostringstream syntax below.
       //Looks like "+" is going to be my best bet, from initial research.
       selectQuery = "SELECT n.id AS nid " + //,s.id,n.name,f.path " +
                       "FROM osmNode n " +
                       //"LEFT JOIN shape s ON n.id = s.node_id " +
                       //"LEFT JOIN shapeFile f ON f.id = s.file_id " +
                       //"JOIN vector3 p ON p.id = s.pos_id " +
                       //"JOIN rotation r ON r.id = s.rot_id " +
                       //"JOIN vector3 sc ON sc.id = s.scale_id " +
                       "WHERE " +// n.type LIKE 'TSSTatic' AND " +
                       "n.cell_name IN  ( ";

       for (int i = 0; i < activeCells.Count; i++)
       {
           if (i < (activeCells.Count - 1))
               selectQuery += "'" + activeCells[i] + "', ";
           else
               selectQuery += "'" + activeCells[i] + "'";
       }
       selectQuery += " );";

       Debug.Log(selectQuery);

       //selectQuery = "SELECT id,osm_id,name FROM osmNode WHERE id<10;";
       //selectQuery = "SELECT id,node_id FROM shape;";
       mDbCmd.CommandText = selectQuery;
       IDataReader reader = mDbCmd.ExecuteReader();
       while (reader.Read())
       {
           int id = Int32.Parse(reader["nid"].ToString()); //reader.GetInt32(0);// reader.GetInt32(0);
                                                           //int s_id = Int32.Parse(reader["s.id"].ToString());
                                                           //    string n_name = reader.GetString(1);
           Debug.Log("id = " + id);// + ",  s_id = " + s_id );
       }
       reader.Close();

}

//NOW, we have a list of cellnames, let's use them:
if (mSQL)
{
    long nodeId,fileId,posId,rotId,scaleId,shapeId;
    int result,total_query_len=0;
    double nodeLong,nodeLat;
    std::string shapeFile,name;
    std::ostringstream selectQuery;
    sqlite_resultset *resultSet;
    //"WHERE n.type LIKE 'TSSTatic' AND " <<
        //"n.cellName IN  ( ";
    selectQuery <<
        "SELECT n.id,s.id,n.name,f.path,p.x,p.y,p.z,r.x,r.y,r.z,r.w,sc.x,sc.y,sc.z " <<
        "FROM osmNode n " <<
        "JOIN shape s ON n.id = s.node_id " <<
        "JOIN shapeFile f ON f.id = s.file_id " <<
        "JOIN vector3 p ON p.id = s.pos_id " <<
        "JOIN rotation r ON r.id = s.rot_id " <<
        "JOIN vector3 sc ON sc.id = s.scale_id " <<
        "WHERE n.type LIKE 'TSSTatic' AND " <<
        "n.cell_name IN  ( ";

    for (int i=0;i<activeCells.size();i++)
    {
        if (i<(activeCells.size()-1))
            selectQuery << "'" << activeCells[i].c_str() << "', ";
        else
            selectQuery << "'" << activeCells[i].c_str() << "' ";

    }
    selectQuery << " );";

    //int queryTime = clock();
    result = mSQL->ExecuteSQL(selectQuery.str().c_str());
    if (result)
    {
        resultSet = mSQL->GetResultSet(result);
        selectQuery.clear();
        selectQuery.str("");
        if (resultSet->iNumRows > 0)
        {
            //Con::printf("found %d staticShapes, query took %d milliseconds",resultSet->iNumRows,clock()-queryTime);
            SimSet* missionGroup = NULL;
            missionGroup = dynamic_cast<SimSet*>(Sim::findObject("MissionGroup"));

            for (int i=0;i<resultSet->iNumRows;i++)
            {
                int c=0;
                float x,y,z,w;
                Vector3 pos,scale;
                QuatF rot;

                nodeId = dAtol(resultSet->vRows[i]->vColumnValues[c++]);//This has to be long, because it's coming from OSM and might be huge.
                shapeId = dAtoi(resultSet->vRows[i]->vColumnValues[c++]);//This is simply an autoincrement from our own table, unlikely to exceed int limits.
                name =  resultSet->vRows[i]->vColumnValues[c++];
                shapeFile = resultSet->vRows[i]->vColumnValues[c++];
                x = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                y = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                z = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                pos = Vector3(x,y,z);

                x = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                y = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                z = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                w = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                rot = QuatF(x,y,z,w);

                x = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                y = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                z = dAtof(resultSet->vRows[i]->vColumnValues[c++]);
                scale = Vector3(x,y,z);

                if (mStaticShapes.find(shapeId) == mStaticShapes.end())
                {
                    TSStatic *shape = new TSStatic();
                    MatrixF transform;
                    rot.setMatrix(&transform);
                    transform.setPosition(pos);
                    shape->setTransform(transform);
                    shape->setScale(scale);
                    shape->mShapeName = StringTable->insert(shapeFile.c_str());
                    shape->mOseId = shapeId;

                    shape->registerObject( name.c_str() );

                    missionGroup->addObject(shape);
                    mStaticShapes[shapeId] = shape;
                }
            }
        } 
    } else Con::printf("Static shape query found no results.");
}

unsigned int latency = clock() - startTime;
Con::printf("makeStaticShapes() took %d milliseconds",latency); 

} **/
/*
                            XmlDocument xmlVersionFile = new XmlDocument();
                            xmlVersionFile.LoadXml(versionXML.text);

                            string versionData = "v. ";
                            foreach (XmlNode node in xmlVersionFile.DocumentElement.ChildNodes)
                            {
                                string nodeName = node.Name;
                                string nodeValue = node.InnerText;
                                versionData += node.InnerText;
                                if (!nodeName.Equals("micro"))
                                    versionData += ".";
                                //Debug.Log("XML VERSION FILE NODE: " + nodeName + " value " + nodeValue);// + " value " + nodeValue.ToString());
                            }

                            versionText.text = versionData;
 */


/*  //Hmm, downloaded this script for placing grass by splat map index. It doesn't seem to get to the part where it actually 
 *   //adds the grass object, but might be a useful reference anyway.
 *   
public class GrassCreator : ScriptableWizard
{

    public Terrain terrain;
    public int detailIndexToMassPlace;
    public int[] splatTextureIndicesToAffect;
    public int detailCountPerDetailPixel = 0;

    [MenuItem("Terrain/Mass Grass Placement")]

    static void createWizard()
    {

        ScriptableWizard.DisplayWizard("Select terrain to put grass on", typeof(GrassCreator), "Place Grass on Terrain");

    }

    void OnWizardCreate()
    {

        if (!terrain)
        {
            Debug.Log("You have not selected a terrain object");
            return;
        }

        if (detailIndexToMassPlace >= terrain.terrainData.detailPrototypes.Length)
        {
            Debug.Log("You have chosen a detail index which is higher than the number of detail prototypes in your detail libary. Indices starts at 0");
            return;
        }

        if (splatTextureIndicesToAffect.Length > terrain.terrainData.splatPrototypes.Length)
        {
            Debug.Log("You have selected more splat textures to paint on, than there are in your libary.");
            return;
        }

        for (int i = 0; i < splatTextureIndicesToAffect.Length; i++)
        {
            if (splatTextureIndicesToAffect[i] >= terrain.terrainData.splatPrototypes.Length)
            {
                Debug.Log("You have chosen a splat texture index which is higher than the number of splat prototypes in your splat libary. Indices starts at 0");
                return;
            }
        }

        if (detailCountPerDetailPixel > 16)
        {
            Debug.Log("You have selected a non supported amount of details per detail pixel. Range is 0 to 16");
            return;
        }

        int alphamapWidth = terrain.terrainData.alphamapWidth;
        int alphamapHeight = terrain.terrainData.alphamapHeight;
        int detailWidth = terrain.terrainData.detailResolution;
        int detailHeight = detailWidth;

        float resolutionDiffFactor = (float)alphamapWidth / detailWidth;


        float[,,] splatmap = terrain.terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);


        int[,] newDetailLayer = new int[detailWidth, detailHeight];

        //loop through splatTextures
        for (int i = 0; i < splatTextureIndicesToAffect.Length; i++)
        {

            //find where the texture is present
            for (int j = 0; j < detailWidth; j++)
            {

                for (int k = 0; k < detailHeight; k++)
                {

                    float alphaValue = splatmap[(int)(resolutionDiffFactor * j), (int)(resolutionDiffFactor * k), splatTextureIndicesToAffect[i]];

                    newDetailLayer[j, k] = (int)Mathf.Round(alphaValue * ((float)detailCountPerDetailPixel)) + newDetailLayer[j, k];

                }

            }

        }

        terrain.terrainData.SetDetailLayer(0, 0, detailIndexToMassPlace, newDetailLayer);

    }

    void OnWizardUpdate()
    {
        helpString = "Ready";




    }

}*/


/*
           //////////////////////////////

               mTerrainData[j, i] = tempData;
               //mTerrainData[j, i].save()??? How do we write this to a file?

               mTerrains[j, i] = Terrain.CreateTerrainGameObject(mTerrainData[j, i]);
               mTerrainsLayout[j, i] = mTerrains[j, i];
       //mTerrains[j,i].save()??? How do we write this to a file?
       ////////////////////////////////



       block->mSquareSize = mD.mSquareSize;
       block->mBaseTexSize = mD.mTextureRes;
       block->mLightMapSize = mD.mLightmapRes;
       block->mLongitude = startLong;
       block->mLatitude = startLat;
       Vector3 blockPos = Vector3(((startLong-mD.mMapCenterLongitude)*mD.mMetersPerDegreeLongitude),
                           (startLat-mD.mMapCenterLatitude)*mD.mMetersPerDegreeLatitude,0.0);//FIX! need maxHeight/minHeight
       block->setPosition(blockPos );

       mTerrains.increment();
       mTerrains.last() = block;

       block->registerObject( terrainName );
       block->addToScene();

       if (terrExists==false)
       {
           if (block->loadTerrainData(heightfilename,texturefilename,mD.mTextureRes,mTerrainMaterials.size(),"treefile.txt"))
               Con::printf("block loaded terrain data: %s",heightfilename);
           else {
               Con::printf("block failed to load terrain data: %s",heightfilename);
           }
       } else {
           Con::printf("block reloaded existing terrain file: %s",terrFileName.c_str());		
       }
        */





/*
fs.setPosition(0 * sizeof(float)); fs.read(&data);
float fileLong = data;
fs.setPosition(1 * sizeof(float)); fs.read(&data);
float fileLat = data;
fs.setPosition(2 * sizeof(float)); fs.read(&data);
float fileTileWidth = data;
fs.setPosition(3 * sizeof(float)); fs.read(&data);
int fileHeightmapRes = (int)data;
fs.setPosition(4 * sizeof(float)); fs.read(&data);
int fileTextureRes = (int)data;
*/

//STOP! Time to check out TerrainMaster::loadTerrainData().

/*
float data;
int numHeightBinArgs = 5;

TerrainFile* terrFile = getFile();

if (!fs.open(heightFile, Torque::FS::File::Read))
{
    for (int xx = 0; xx < getBlockSize(); xx++)
    {
        for (int yy = 0; yy < getBlockSize(); yy++)
        {
            setHeight(Point2I(xx, yy), 0.0f);//for now just set unknown terrain to flat plane at zero meters elevation.
                                             //mTerrainsLayout[y][x]->setHeight(Point2I(xx,yy),data);
        }
    }
    return false;
}
else
{
    fs.setPosition(0 * sizeof(float)); fs.read(&data);
    float fileLong = data;
    fs.setPosition(1 * sizeof(float)); fs.read(&data);
    float fileLat = data;
    fs.setPosition(2 * sizeof(float)); fs.read(&data);
    float fileTileWidth = data;
    fs.setPosition(3 * sizeof(float)); fs.read(&data);
    int fileHeightmapRes = (int)data;
    fs.setPosition(4 * sizeof(float)); fs.read(&data);
    int fileTextureRes = (int)data;
    if (fileHeightmapRes != getBlockSize())
    {
        Con::printf("Wrong heightmap resolution in file: %s", heightFile);
        //for(int i=0; i<mTerrainsXCount; i++)//First time, we're going to have to load all terrains for sure.
        //	for(int j=0; j<mTerrainsZCount; j++)
        //		mLoadTerrains[j][i] = true; 
        //pingWorldServer(true);
        return false;
    }
    //Con::printf("Loading terrain data from:  %s, long/lat: %f %f  heightmapres %d  tileWidth %f",
    //	fileName,fileLong,fileLat,fileHeightmapRes,getWorldBlockSize());

    for (int xx = 0; xx < getBlockSize(); xx++)
    {
        for (int yy = 0; yy < getBlockSize(); yy++)
        {
            fs.setPosition((yy * getBlockSize() * sizeof(float)) + (xx * sizeof(float)) + (numHeightBinArgs * sizeof(float)));
            fs.read(&data);
            setHeight(Point2I(xx, yy), data);
        }
    }
    fs.close();
}

U8 texData;
if (fs.open(textureFile, Torque::FS::File::Read))
{
    //HERE: This needs a whole new system, instead of mapping flightgear layers to individual T3D layers, we need to use
    // the flightgear layer to map to a whole set of T3D textures, assigned via internal logic specific to area type.
    for (int xx = 0; xx < getBlockSize(); xx++)
    {
        for (int yy = 0; yy < getBlockSize(); yy++)
        {
            fs.setPosition((yy * getBlockSize() * sizeof(U8)) + (xx * sizeof(U8)));
            fs.read(&texData);

            //HMMM. What we should really be doing here is defining areas of a certain type, and then later run through
            //these regions one by one and define them using local rules.

            //Old way - just map textures 1 to 1
            U8 index = texData - 1;//Flightgear puts out 1-based numbers, here we are 0-based.
            if ((index >= 0) && (index < material_count))
                terrFile->setLayerIndex(xx, yy, index);
        }
    }
//for (int xx=0;xx<textureRes;xx++)
//{	
//	for (int yy=0;yy<textureRes;yy++)
//	{
//		fs.setPosition((yy*textureRes*sizeof(U8)) + (xx*sizeof(U8)));
//		fs.read(&texData);
//		U8 index = texData - 1;//Flightgear puts out 1-based numbers, here we are 0-based.
//		if ((index >= 0)&&(index < material_count))
//			terrFile->setLayerIndex( xx, yy, index );
//	}
//}

    fs.close();
}

updateGrid(Point2I(0, 0), Point2I(getBlockSize() - 1, getBlockSize() - 1), true);
//updateGridMaterials(Point2I(0,0),Point2I(getBlockSize()-1,getBlockSize()-1));

terrFile->save(terrFile->mFilePath.getFullPath());
*/
