using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DW_DeveloperKeyWarning : MonoBehaviour
{
    [SerializeField] private Button label,modal;

    private void Awake()
    {
        label.onClick.AddListener(()=> modal.gameObject.SetActive(true));
        modal.onClick.AddListener(()=>modal.gameObject.SetActive(false));
    }
}
