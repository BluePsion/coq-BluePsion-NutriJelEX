using System;
using System.Collections.Generic;
using XRL.Core;
using XRL.UI;
using System.Text;
using XRL.Rules;
using ConsoleLib.Console;
using XRL.Messages;
using XRL.World;


/* 
The NutriJelEx allows the waunderer to turn corpses into a convenient food source using battery power.
*/

namespace XRL.World.Parts
{
  [Serializable]
  public class BP_NutriJelExtruder : IPart
  {
    public int extrudethreshold = 21000; //Avg Corpse: Hunger = 20000. 5% loss.                                            
    public int nutrientlvl;              //Current level of nutrients stored in device.
    public int processCost = 200;        //Determines energy cost for corpses. Cost is a function of this variable and the hunger a food object satisfies.
                                            //At value 200, This will use 100 charge for a 20000 hunger corpse.
    public BP_NutriJelExtruder()
    {
      this.Name = "BP_NutriJelExtruder";
      
    }

    public override bool SameAs(IPart p)
    {
      return false;
    }

    public override void Register(GameObject Object)
    {
      Object.RegisterPartEvent((IPart) this, "GetInventoryActions");
      Object.RegisterPartEvent((IPart) this, "InvCommandInsertCorpse");
    }

    public override bool FireEvent(Event E)
    {
      string foodItemBlueprint; //Used to replace items taken but not used.
      
      if (E.ID == "GetInventoryActions")
      {
        (E.GetParameter("Actions") as EventParameterGetInventoryActions).AddAction("insert", 'i', false, "&Wi&ynsert nutrients", "InvCommandInsertCorpse");
        return true;
      }
      
      if (E.ID == "InvCommandInsertCorpse")
      {
        GameObject gameObject1 = E.GetParameter("Owner") as GameObject; //this should fetch the player object
        if (gameObject1 == null) 
          return true;
        if (!gameObject1.HasPart("Inventory")) //has to have an inventory
          return true;
        if (!gameObject1.IsPlayer()) //has to be the player.
          return false; //Not even sure what this does. Is it just returning if the event is handled or not?
        
        
        ///Get food item choice from player/////////////////////////////////////////////////////////
        Inventory inventory = gameObject1.GetPart("Inventory") as Inventory;
        Dictionary<char, GameObject> dictionary = new Dictionary<char, GameObject>();
        
        char key = 'a';
        string Message = "";
        
        foreach (GameObject @object in inventory.GetObjects())
        {
          //Understood means that the player knows what this item is.  Players can only misunderstand
          //artifacts so this should be unnecessary for food items but leaving this for now.
          if (@object.Understood() && @object.GetPart("Food") != null)
          {
            dictionary.Add(key, @object);
            Message = Message + (object) key + ") " + @object.DisplayName + "\n";
            ++key;
          }
        }
        
        
        if (Message == "")
        {
          if (gameObject1.IsPlayer())
            Popup.Show("You have nothing to feed into the NutriJelEX!", true);
          return false;
        }
        Message += "&Wspace&y go back\n";
        int num = Popup.ShowChoice(Message);
        if (num == 32) //space.
        {
          return true;
        }
        
        ////////////////////////////////////////////////////////////////////////////////////////////
        
        //Ask for how many//////////////////////////////////////////////////////////////////////////
        int numberOfItems = 0; //total number of stack of items.
        int numberProcessItems = 1; //number of items player wants to feed into the NutriJelEX
        
        if (dictionary.ContainsKey((char) num))
        {
          GameObject foodItem = dictionary[(char) num];
          foodItemBlueprint = foodItem.Blueprint;
          
          if(foodItem.HasPart("Stacker"))
          {
            
            Stacker stacker = foodItem.GetPart("Stacker") as Stacker;
            numberOfItems = stacker.Number;
           
           
           if(numberOfItems > 1)
           {
             
             string str = Popup.AskString("How many do you want to give to the machine?(max=" + (object) stacker.Number + ")", stacker.Number.ToString(), 5);
             
             try
              {
                numberProcessItems = Convert.ToInt32(str);
              }
              catch
              {
                numberProcessItems = numberOfItems;
              }
              if (numberProcessItems <= 0)
                return true;
              if (numberProcessItems >= numberOfItems)
              {
                numberProcessItems = numberOfItems;
              }
           }
              if(numberOfItems>1)
              {
              Event E1 = Event.New("SplitStack", "Number", numberProcessItems);
              E1.AddParameter("OwningObject", (object) XRLCore.Core.Game.Player.Body);
              foodItem.FireEvent(E1);
              inventory.FireEvent(Event.New("CommandRemoveObject", "Object", (object) foodItem, "ForEquip", (object) false));
              }else
              {
                inventory.FireEvent(Event.New("CommandRemoveObject", "Object", (object) foodItem, "ForEquip", (object) false));
              }
            
            
            
          }//Doesn't have Stacker part: All food has a stacker so this is unnecessary for now.
          /*else
          {
            //remove it, but we still have reference...
            inventory.FireEvent(Event.New("CommandRemoveObject", "Object", (object) foodItem, "ForEquip", (object) false));
            numberProcessItems = 1;
          }*/
          
          //Create food slabs.
          Food food = foodItem.GetPart("Food") as Food;
          int foodSlabsExtruded = 0;
          bool ranOutOfPower = false;
              while(numberProcessItems > 0)
              {
                
                //Average corpse is 20000 hunger, using that, calculate how much energy this piece of food should take to process.
                int chargeToProcess = 0;
                  
                chargeToProcess = food.Hunger / processCost; 
                //Popup.Show("Cost to process: " + chargeToProcess.ToString(),true);
                
                
                if(this.ParentObject.UseCharge(chargeToProcess))
                {
                  numberProcessItems = numberProcessItems - 1;
                  nutrientlvl = nutrientlvl + food.Hunger;
                
                while(nutrientlvl >= extrudethreshold)
                {
                  
                  //Make the food jell and put it in player inventory
                  nutrientlvl = nutrientlvl - extrudethreshold;
                  GameObject nutriObject = GameObjectFactory.Factory.CreateObject("BP NutriJel Slab");
                  inventory.AddObject(nutriObject);
                  foodSlabsExtruded++;
                  
                }
              }else
              {
                ranOutOfPower = true;
                //Popup.Show("One of the machine's bulbs glows red. The intake tray jams.",true);
                break;
              }
              }
              
              //Provide result to waunderer
              if(foodSlabsExtruded>1)
              {
                string feedback = "A food slab slides from the output tray.";
                if(foodSlabsExtruded > 1)feedback = feedback + " (x"+foodSlabsExtruded.ToString()+")";
                Popup.Show(feedback,true);
                XRL.Messages.MessageQueue.AddPlayerMessage(feedback);
              }
              if(ranOutOfPower)
              {
                XRL.Messages.MessageQueue.AddPlayerMessage("One of the machine's bulbs glows red and the intake jams.");
                Popup.Show("One of the machine's bulbs glows red and the intake jams.",true);
              }
              
              //Replace any items that were not able to be processed due to low/missing battery.
              while(numberProcessItems > 0)
              {
                numberProcessItems--;
                inventory.AddObject(GameObjectFactory.Factory.CreateObject(foodItemBlueprint));
                //Popup.Show("Failed to insert all nutrients.",true);
                
              }                              
        }//End item in dictionary
      }
      return true;
    }
  }
}
