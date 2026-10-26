#pragma strict

var Model : GameObject ;


function Start () {
    Model.GetComponent.<Renderer>().material.color = Color.white;
}

function Update () {

    if(Input.GetKey(KeyCode.Space))
    {
        Model.GetComponent.<Renderer>().material.color = Color.red;
    }
    else
    {
        Model.GetComponent.<Renderer>().material.color = Color.white;
        
    }
     
}