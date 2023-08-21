using System;
using UnityEngine;
using UnityEngine.Subsystems;
using Config;

//при смене id с помощью кода изменения не сохраняются
//необходимо самостоятельно сохранить сцену
//Simulator >> Save Scene

public class ConfigData : MonoBehaviour, Config.IID
{
    [SerializeField, HideInInspector]
    private int uniqueID = generateID();
    
    public string UniqueID => return GetType().Name + '_' + uniqueID.ToString();

    private static int generateID()
    {
        var rand = new System.Random();
        int value = rand.Next(100,999);
        return value;
    }

    public int getID()
    {
        return uniqueID;
    }
}