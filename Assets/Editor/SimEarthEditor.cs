using UnityEngine;
using UnityEditor;

using System.Data;
using Mono.Data.Sqlite;
using System.Linq;

public class SimEarthEditorMenu
{


    [MenuItem("SimEarth/RunTerrainPager", false, 1)]
    private static void simEarthRunTerrainPager()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        theTP.RunTerrainPager();
    }

    [MenuItem("SimEarth/StopTerrainPager",false,2)]
    private static void simEarthStopTerrainPager()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        theTP.StopTerrainPager();
    }
    /*
    [MenuItem("SimEarth/MakeRoad", false, 3)]
    private static void simEarthMakeRoad()
    {
        TerrainPager theTP;
        theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        theTP.MakeARoad();
    }*/

    [MenuItem("SimEarth/ImportOpenStreetMap", false, 21)]
    private static void simEarthImportOpenStreetMap()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        //FIX: put up a browser window to find a file.
        theTP.ImportOpenStreetMap("Assets/OSM/creswell_greater.osm");
    }

    [MenuItem("SimEarth/SaveRoads", false, 22)]
    private static void simEarthSaveRoads()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        theTP.SaveRoads();
    }

    [MenuItem("SimEarth/DeleteRoads", false, 23)]
    private static void simEarthDeleteRoads()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        //theTP.DeleteRoads();
    }


    //FIX: move all this into terrainPager, don't do it here.
    [MenuItem("SimEarth/LoadShapes", false, 331)]
    private static void simEarthLoadShapes()
    {
        TerrainPager theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;


        //Have to load up values in terrainPager, it does this at runtime. Better way??
        theTP.mD.mSquareSize = 10.0f;
        theTP.mD.mHeightmapRes = 257;//TEMP, 257, FiX FIX FIX, have to get back into FG to fix this though.
        theTP.mD.mTileWidth = (float)(theTP.mD.mHeightmapRes - 1) * theTP.mD.mSquareSize;
        theTP.mD.mMapCenterLatitude = 44.0f;// 21.936f;//22.0f;////HERE: this is the geographic center of the whole map.
        theTP.mD.mMapCenterLongitude = -123.0047f;// 123.0047f;//  -159.380f;//-159.5f;//GET THIS FROM THE GUI! //.005?? Something is broken, by just five thousandths. ??

        float rLat = theTP.mD.mMapCenterLatitude * Mathf.Deg2Rad;
        theTP.mD.mMetersPerDegreeLatitude = 111132.92f - 559.82f * Mathf.Cos(2 * rLat) + 1.175f * Mathf.Cos(4 * rLat);
        theTP.mD.mMetersPerDegreeLongitude = 111412.84f * Mathf.Cos(rLat) - 93.5f * Mathf.Cos(3 * rLat);
        theTP.mD.mDegreesPerMeterLongitude = 1.0f / theTP.mD.mMetersPerDegreeLongitude;
        theTP.mD.mDegreesPerMeterLatitude = 1.0f / theTP.mD.mMetersPerDegreeLatitude;
        theTP.mD.mTileWidthLongitude = theTP.mD.mDegreesPerMeterLongitude * theTP.mD.mTileWidth;
        theTP.mD.mTileWidthLatitude = theTP.mD.mDegreesPerMeterLatitude * theTP.mD.mTileWidth;


        string mDbName = "w130n40.db";
        string conn = "URI=file:" + Application.dataPath + "/" + mDbName;//Will this break on build as well? Move to Resources?
        string selectQuery, upsertQuery;
        
        IDbConnection mDbConn;
        IDbCommand mDbCmd;
        IDataReader reader;

        mDbConn = (IDbConnection)new SqliteConnection(conn);
        mDbConn.Open(); //Open connection to the database.
        mDbCmd = mDbConn.CreateCommand();

        Coordinates terrCoord = new Coordinates();
        Coordinates nodeCoord = new Coordinates();
        Coordinates objCoord = new Coordinates();

        GameObject[] objs = Selection.gameObjects;
        GameObject[] allObjs = Object.FindObjectsOfType<GameObject>();
        int terrainCount = 0;
        foreach (GameObject terrObj in objs)
        {
            Terrain terr = terrObj.GetComponent<Terrain>();
            if (terr != null)
            {
                TerrainData terrData = terr.terrainData;
                if (terrData.name.IndexOf('.') <= 0) // if we are not on a proper tile that came from a terrainData asset.
                   continue;

                Vector3 terrPos = terr.transform.position;
                terrainCount++;

               string tileName = terrData.name.Substring(terrData.name.IndexOf('.') + 1);
               string objName = "";
                int nodeId = 0;
               //Debug.Log("Terrain selected! tile name " + tileName + "   all objects: " + allObjs.Length +
               //    " mapNodes " + mapNodeObjs.Length + " players " + playerObjs.Length + " peasants " + peasantObjs.Length);
               selectQuery = "SELECT latitude,longitude FROM mapNode WHERE name='" + tileName + "' AND type='Terrain';";//type='Terrain' is probably overkill, but why not.
               mDbCmd.CommandText = selectQuery;
               reader = mDbCmd.ExecuteReader();
               //Debug.Log(selectQuery);
               while (reader.Read())
               {
                   //Playing fast and loose here but I KNOW I gave everybody a latitude and longitude...
                   terrCoord.latitude = reader.GetDouble(reader.GetOrdinal("latitude"));
                   terrCoord.longitude = reader.GetDouble(reader.GetOrdinal("longitude"));
               }
               reader.Close();


               selectQuery = "SELECT id,latitude,longitude,name FROM mapNode WHERE type='MapNode' AND latitude >= " + terrCoord.latitude +
                             " AND latitude < " + (terrCoord.latitude + theTP.mD.mTileWidthLatitude) + " AND longitude >= " +
                             terrCoord.longitude + " AND longitude < " + (terrCoord.longitude + theTP.mD.mTileWidthLongitude) + ";";
               mDbCmd.CommandText = selectQuery;
               reader = mDbCmd.ExecuteReader();
               while (reader.Read())
               {
                    nodeId = reader.GetInt32(reader.GetOrdinal("id"));
                    nodeCoord.latitude = reader.GetDouble(reader.GetOrdinal("latitude"));
                    nodeCoord.longitude = reader.GetDouble(reader.GetOrdinal("longitude"));
                    try { objName = reader.GetString(reader.GetOrdinal("name")); }
                    catch { }

                    if (nodeId <= 0)
                        continue;

                    Debug.Log("Loading map node: " + nodeCoord.longitude + "   " + nodeCoord.latitude);

                    GameObject myNode = GameObject.Instantiate(theTP.MapNodePrefab);
                    Vector3 myPos = new Vector3();
                    myPos = theTP.ConvertLatLongToXYZ(nodeCoord.longitude, 0, nodeCoord.latitude);
                    myPos.y = terr.SampleHeight(myPos);
                    myNode.transform.position = myPos;
                    myNode.GetComponent<SimBase>().id = nodeId;
                    //.y = terrData.GetHeight();
                    //Debug.Log("!!!!!!!!!!!!!!! " + tileName + " INSTANTIATED a mapnode!!!!! " + objName + " pos " + myNode.transform.position + "!!!!!!!!!!!!!");
                    myNode.name = objName;

                    //Debug.Log(tileName + " found a mapnode! " + objName + "  " + objCoord.longitude + " " + objCoord.latitude);
                    //Next, select all mapShapes associated with this node...
                    //And then, for each shape, find the appropriate file.


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
                        string pathQuery = "SELECT path FROM mapShapeFile WHERE id=" + file_id + ";";
                        //Debug.Log(pathQuery);
                        IDbCommand mDbCmd3 = mDbConn.CreateCommand();
                        mDbCmd3.CommandText = pathQuery;
                        IDataReader subSubReader = mDbCmd3.ExecuteReader();
                        subSubReader.Read();
                        string path = subSubReader.GetString(subSubReader.GetOrdinal("path"));
                        subSubReader.Close();
                        subSubReader.Dispose();
                        mDbCmd3.Dispose();

                        if (path.Length == 0)
                            continue;

                        //Debug.Log("Should be creating prefab: " + path);
                        //And now we have the path, since I did *not* do a join in the original query for lazy and stupid reasons. (FIX FIX FIX)
                        UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath(path + ".prefab", typeof(GameObject));
                        GameObject myObj = GameObject.Instantiate(prefab) as GameObject;
                        Vector3 objPos = myNode.transform.position + pos;
                        objPos.y = terr.SampleHeight(objPos);
                        myObj.transform.position = objPos;
                        myObj.transform.rotation = q; // Ah, hmm, as currently recorded, map nodes don't have any rotation. This might suck.
                        myObj.transform.localScale = scale;//Ditto with scale.
                        myObj.GetComponent<SimBase>().id = id;
                        myObj.GetComponent<SimBase>().file_id = file_id;
                        myObj.GetComponent<SimBase>().mapNode = myNode;
                    }
                    subReader.Close();
                    subReader.Dispose();
                    mDbCmd2.Dispose();
                }
                reader.Close();              
            }
        }
    }


    [MenuItem("SimEarth/UnloadShapes", false, 332)]
    private static void simEarthUnloadShapes()
    {

        GameObject[] allObjs = Object.FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjs)
        {
            if (obj.GetComponent<SimBase>() != null)
                if (obj.GetComponent<SimBase>().id > 0)
                    GameObject.DestroyImmediate(obj);//Is this an at all safe way of doing this, at all?
        }
    }


    //FIX: move all this into terrainPager, don't do it here.
    [MenuItem("SimEarth/SaveShapes", false, 333)]
    private static void simEarthSaveShapes()
    {
        TerrainPager theTP;
        theTP = GameObject.Find("TerrainPager").GetComponent<TerrainPager>();
        if (theTP == null)
            return;

        //Have to load up values in terrainPager, it does this at runtime. Better way??
        theTP.mD.mSquareSize = 10.0f;
        theTP.mD.mSkyboxRes = 800;
        theTP.mD.mHeightmapRes = 257;//TEMP, 257, FiX FIX FIX, have to get back into FG to fix this though.
        theTP.mD.mTileWidth = (float)(theTP.mD.mHeightmapRes - 1) * theTP.mD.mSquareSize;
        theTP.mD.mMapCenterLatitude = 44.0f;// 21.936f;//22.0f;////HERE: this is the geographic center of the whole map.
        theTP.mD.mMapCenterLongitude = -123.0047f;// 123.0047f;//  -159.380f;//-159.5f;//GET THIS FROM THE GUI! //.005?? Something is broken, by just five thousandths. ??
        
        float rLat = theTP.mD.mMapCenterLatitude * Mathf.Deg2Rad;
        theTP.mD.mMetersPerDegreeLatitude = 111132.92f - 559.82f * Mathf.Cos(2 * rLat) + 1.175f * Mathf.Cos(4 * rLat);
        theTP.mD.mMetersPerDegreeLongitude = 111412.84f * Mathf.Cos(rLat) - 93.5f * Mathf.Cos(3 * rLat);
        theTP.mD.mDegreesPerMeterLongitude = 1.0f / theTP.mD.mMetersPerDegreeLongitude;
        theTP.mD.mDegreesPerMeterLatitude = 1.0f / theTP.mD.mMetersPerDegreeLatitude;
        theTP.mD.mTileWidthLongitude = theTP.mD.mDegreesPerMeterLongitude * theTP.mD.mTileWidth;
        theTP.mD.mTileWidthLatitude = theTP.mD.mDegreesPerMeterLatitude * theTP.mD.mTileWidth;

        string mDbName = "w130n40.db";
        string conn = "URI=file:" + Application.dataPath + "/" + mDbName;//Will this break on build as well? Move to Resources?
        string selectQuery, upsertQuery;

        IDbConnection mDbConn;
        IDbCommand mDbCmd;

        mDbConn = (IDbConnection)new SqliteConnection(conn);
        mDbConn.Open(); //Open connection to the database.
        mDbCmd = mDbConn.CreateCommand();

        Debug.Log("Saving shapes for the selected terrain!");
        GameObject[] allObjs = Object.FindObjectsOfType<GameObject>();
        GameObject[] untaggedObjs = GameObject.FindGameObjectsWithTag("Untagged");
        GameObject[] playerObjs = GameObject.FindGameObjectsWithTag("Player");
        GameObject[] peasantObjs = GameObject.FindGameObjectsWithTag("Peasant");
        GameObject[] mapNodeObjs = GameObject.FindGameObjectsWithTag("MapNode");
        GameObject[] wildlifeObjs = GameObject.FindGameObjectsWithTag("Wildlife");

        Coordinates terrCoord = new Coordinates();
        Coordinates objCoord = new Coordinates();

        //GameObject[] objs = Selection.gameObjects;
        bool foundTerrain = false;
        int terrainCount = 0;
        foreach (GameObject obj in allObjs)
        {
            Terrain terr = obj.GetComponent<Terrain>();
            if (terr != null)
            {
                TerrainData terrData = terr.terrainData;
                if (terrData == null)
                    continue;

                Vector3 terrPos = terr.transform.position;
                if (terrData.name.IndexOf('.') <= 0) // TEMP - if we are not on a proper tile that came from a terrainData asset, 
                    continue;                           //but let's fix the BaseTerrain so this never happens.

                foundTerrain = true;
                terrainCount++;

                string tileName = terrData.name.Substring(terrData.name.IndexOf('.')+1);
                //Debug.Log("Terrain selected! tile name " + tileName + "   all objects: " + allObjs.Length +
                //    " mapNodes " + mapNodeObjs.Length + " players " + playerObjs.Length + " peasants " + peasantObjs.Length);
                selectQuery = "SELECT latitude,longitude FROM mapNode WHERE name='" + tileName + "' AND type='Terrain';";//type='Terrain' is probably overkill, but why not.
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

                foreach (GameObject n in mapNodeObjs)
                {
                    Object myPrefab = PrefabUtility.GetCorrespondingObjectFromSource(n);
                    string myPath = AssetDatabase.GetAssetPath(myPrefab);
                    Vector3 nPos = n.transform.position;
                    Vector3 relPos = nPos - terrPos;
                    int nodeId = 0;
                    if (n.GetComponent<SimBase>() != null)
                        nodeId = n.GetComponent<SimBase>().id;

                    //If we don't exist in the db, insert.

                    //FIX: loop through all terrains in the scene
                    //Make sure we are within the bounds of this terrain.
                    if ((relPos.x > terr.terrainData.size.x) || (relPos.z > terr.terrainData.size.z))
                        continue;



                    //Okay I think conversion to floats here is causing trouble, let's stop doing it.
                    //Vector3 terrMapPos = theTP.ConvertLatLongToXYZ(terrCoord.longitude, 0, terrCoord.latitude);
                    //Coordinates objCoords = theTP.ConvertXYZToLatLong(terrMapPos + relPos);

                    Coordinates relCoords = new Coordinates();//Can we do this on a difference rather than actual coordinates? Hm.
                    relCoords.longitude = relPos.x * theTP.mD.mDegreesPerMeterLongitude;
                    relCoords.latitude = relPos.z * theTP.mD.mDegreesPerMeterLatitude;
                    Coordinates objCoords = new Coordinates();
                    objCoords.longitude = terrCoord.longitude + relCoords.longitude;
                    objCoords.latitude = terrCoord.latitude + relCoords.latitude;
                    Debug.Log("** ObjCoord: " + objCoord.longitude + " " + objCoord.latitude  + " relCoord " + relCoords.longitude + " " + relCoords.latitude);
                    if (nodeId <= 0)                    
                        upsertQuery = "INSERT INTO mapNode (latitude,longitude,name,type) VALUES (" + objCoords.latitude + "," + objCoords.longitude +
                                        ",'" + n.name + "','MapNode');";                                        
                    else //else, update
                        upsertQuery = "UPDATE mapNode SET longitude=" + objCoords.longitude + ", latitude=" + objCoords.latitude +
                                        ",name='" + n.name + "' WHERE id=" + nodeId + ";";
                    Debug.Log(upsertQuery);                 
                    mDbCmd.CommandText = upsertQuery;
                    mDbCmd.ExecuteNonQuery();

                    if (nodeId <= 0)
                    {
                        selectQuery = "SELECT last_insert_rowid();";
                        mDbCmd.CommandText = selectQuery;
                        reader = mDbCmd.ExecuteReader();
                        reader.Read();
                        nodeId = reader.GetInt32(0);
                        n.GetComponent<SimBase>().id = nodeId;
                        reader.Close();
                    }
                    //Debug.Log(selectQuery);
                }

                //NOW, mapNodes are guaranteed to exist and be up to date in terms of position. Next, let's save the wildlife shapes, which simply need
                //local positions relative to their mapNode, which they have a link to in their SimBase script so we don't need any queries.
                foreach (GameObject n in wildlifeObjs)
                {
                    int nodeId = 0;
                    GameObject mNode = n.GetComponent<SimBase>().mapNode;
                    if ((mNode == null) || (mNode.GetComponent<SimBase>().id <= 0))
                        continue;

                    nodeId = mNode.GetComponent<SimBase>().id;
                    int shapeId = n.GetComponent<SimBase>().id;
                    Vector3 relPos = n.transform.position - mNode.transform.position;
                    Vector3 scale = n.transform.localScale;
                    Quaternion q = n.transform.rotation;


                    int fileId = n.GetComponent<SimBase>().file_id;
                    if (fileId <= 0)
                    {
                        Object myPrefab = PrefabUtility.GetCorrespondingObjectFromSource(n);
                        string myPath = AssetDatabase.GetAssetPath(myPrefab);
                        if (myPath.IndexOf('.') > 0)
                            myPath = myPath.Remove(myPath.IndexOf('.'));
                        //Debug.Log("Checking for shape file: " + myPath);
                        //Ah, whoops, first gotta check for mapShapeFile!
                        selectQuery = "SELECT id FROM mapShapeFile WHERE path='" + myPath + "';";
                        mDbCmd.CommandText = selectQuery;
                        reader = mDbCmd.ExecuteReader();
                        reader.Read();
                        try { fileId = reader.GetInt32(0); }
                        catch { }
                        reader.Close();

                        if (fileId <= 0)
                        {
                            upsertQuery = "INSERT INTO mapShapeFile (path) VALUES ('" + myPath + "');";
                            mDbCmd.CommandText = upsertQuery;
                            mDbCmd.ExecuteNonQuery();
                            selectQuery = "SELECT last_insert_rowid();";
                            mDbCmd.CommandText = selectQuery;
                            reader = mDbCmd.ExecuteReader();
                            reader.Read();
                            fileId = reader.GetInt32(0);
                            reader.Close();
                            Debug.Log("Inserting shape file, path=" + myPath);
                        }
                        else
                            Debug.Log("Loaded existing shape file, id=" + fileId);

                    }
                    if (shapeId <= 0)
                    {
                        upsertQuery = "INSERT INTO mapShape (node_id,file_id,x,y,z,rx,ry,rz,rw,sx,sy,sz) VALUES (" + nodeId + "," +
                                        fileId + "," + relPos.x + "," + relPos.y + "," + relPos.z + "," + q.x + "," + q.y + "," + q.z + "," + q.w +
                                        "," + scale.x + "," + scale.y + "," + scale.z + ");";
                        mDbCmd.CommandText = upsertQuery;
                        mDbCmd.ExecuteNonQuery();
                        //Debug.Log(upsertQuery);
                        selectQuery = "SELECT last_insert_rowid();";
                        mDbCmd.CommandText = selectQuery;
                        reader = mDbCmd.ExecuteReader();
                        reader.Read();
                        n.GetComponent<SimBase>().id = reader.GetInt32(0);
                        reader.Close();
                    }
                    else
                    {
                        upsertQuery = "UPDATE mapShape SET node_id=" + nodeId + ",file_id=" + fileId + ",x=" + relPos.x + ",y=" + relPos.y + ",z=" + relPos.z + ",rx=" + q.x + ",ry=" + q.y +
                                        ",rz=" + q.z + ",rw=" + q.w + ",sx=" + scale.x + ",sy=" + scale.y + ",sz=" + scale.z + " WHERE id=" + shapeId + ";";
                        mDbCmd.CommandText = upsertQuery;
                        mDbCmd.ExecuteNonQuery();
                        //Debug.Log(upsertQuery);
                    }
                }
            }
        }
        mDbCmd.Dispose();
        mDbConn.Close();
        mDbConn.Dispose();
        Debug.Log("We found " + terrainCount + " terrains!");
    }
}