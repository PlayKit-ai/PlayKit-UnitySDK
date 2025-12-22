using System.Collections;
using System.Collections.Generic;
using PlayKit_SDK.Public;
using UnityEngine;

public class PlayKit_NPCClient_ExampleActions : NpcActionHandlerBase
{

    [SerializeField] private GameObject _exampleShopWindow;
    public List<NpcAction> ActionDefinitions { get; }

    public override string Execute(NpcActionCallArgs args)
    {
        switch (args.ActionName)
        {
            case "open_shop":
                _exampleShopWindow.SetActive(true);
                return "shop opened for player";
                break;
            
        }
        return null;
    }
}
