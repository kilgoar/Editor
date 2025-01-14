﻿using UnityEngine;

[ExecuteAlways]
public class UpdateTerrainValues : MonoBehaviour
{
    Vector3 position = Vector3.zero;
    MapIO mapIO;
    Terrain terrain;
    public void Start()
    {
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }
        if (mapIO == null)
        {
            mapIO = GameObject.FindGameObjectWithTag("MapIO").GetComponent<MapIO>();
        }
    }
    void Update ()
    {
        if (transform.position != position)
        {
            transform.position = position;
        }
        if (transform.localScale != Vector3.one)
        {
            transform.localScale = Vector3.one;
        }
        if (transform.localRotation.eulerAngles != Vector3.zero)
        {
            transform.localRotation = Quaternion.Euler(Vector3.zero);
        }
    }
    public void setPosition(Vector3 position)
    {
        transform.position = position;
        this.position = position;
    }
}
