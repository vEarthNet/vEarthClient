using System.Collections;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;
using UnityEngine;

public class SqliteBase : MonoBehaviour
{

    public string database_name;
    [HideInInspector]
    public string db_connection_string;
    [HideInInspector]
    public IDbConnection db_connection;

    public SqliteBase()
    {
    }

    ~SqliteBase()
    {
        if (db_connection != null)
            db_connection.Close();
    }

    public void Start()
    {
        db_connection_string = "URI=file:" + Application.persistentDataPath + "/" + database_name;
        Debug.Log("db_connection_string" + db_connection_string);
        db_connection = new SqliteConnection(db_connection_string);
        db_connection.Open();
    }

    //vitual functions
    public virtual IDataReader getDataById(int id)
    {
        throw null;
    }

    public virtual IDataReader getDataByString(string str)
    {
        throw null;
    }

    public virtual void deleteDataById(int id)
    {
        throw null;
    }

    public virtual void deleteDataByString(string id)
    {
        throw null;
    }

    public virtual IDataReader getAllData()
    {
        throw null;
    }

    public virtual void deleteAllData()
    {
        throw null;
    }

    public virtual IDataReader getNumOfRows()
    {
        throw null;
    }

    //helper functions
    public IDbCommand getDbCommand()
    {
        return db_connection.CreateCommand();
    }

    public IDataReader getAllData(string table_name)
    {
        IDbCommand dbcmd = db_connection.CreateCommand();
        dbcmd.CommandText =
            "SELECT * FROM " + table_name + ";";
        IDataReader reader = dbcmd.ExecuteReader();
        return reader;
    }

    public void deleteAllData(string table_name)
    {
        IDbCommand dbcmd = db_connection.CreateCommand();
        dbcmd.CommandText = "DELETE FROM " + table_name + ";";
        dbcmd.ExecuteNonQuery();
    }

    public IDataReader getNumOfRows(string table_name)
    {
        IDbCommand dbcmd = db_connection.CreateCommand();
        dbcmd.CommandText =
            "SELECT COALESCE(MAX(id)+1, 0) FROM " + table_name + ";";
        IDataReader reader = dbcmd.ExecuteReader();
        return reader;
    }

    public void close()
    {
        db_connection.Close();
    }
}
