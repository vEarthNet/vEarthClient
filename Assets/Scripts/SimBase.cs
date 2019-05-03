using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Base script that attaches to every world object that exists in the DB.
public class SimBase : MonoBehaviour
{
    public int id = 0;
    [HideInInspector]
    public int file_id = 0;
    public GameObject mapNode = null;
    [HideInInspector]
    public TerrainPager mTP = null;
    [HideInInspector]
    public GameObject mTerrainObject = null;
    [HideInInspector]
    public Terrain mTerrain = null;

    public SimBase()
    {
        //Debug.Log("SimBase constructor! ");
    }

    // Start is called before the first frame update
    public void Start()
    {
        //Debug.Log("SimBase Object starting! " + name);
    }

    // Update is called once per frame
    public void Update()
    {
        if (mTP == null)
        {
            mTP = GameObject.FindWithTag("TerrainPager").GetComponent<TerrainPager>();
            return;
        }       
        else if (mTerrainObject == null)
        {
            mTerrainObject = mTP.GetTerrainBlock(this.transform.position);
            if (mTerrainObject != null)
                mTerrain = mTerrainObject.GetComponent<Terrain>();
        }
        if (mTerrain != null)
        {
            Vector3 newPos = transform.position;
            newPos.y = mTerrain.SampleHeight(newPos);
            this.transform.position = newPos;
        }
        
    }
}
