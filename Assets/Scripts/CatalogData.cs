using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CatalogDatabase", menuName = "Roomify/Catalog Database")]
public class CatalogDatabase : ScriptableObject
{
    public List<CatalogCategory> categories = new();
}

[Serializable]
public class CatalogCategory
{
    public string categoryName;
    public Sprite categoryIcon;
    public List<CatalogItem> items = new();
}

[Serializable]
public class CatalogItem
{
    public string displayName;
    public Sprite thumbnail;
    public GameObject prefab;
}
