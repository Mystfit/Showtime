using UnityEngine;
using System.Collections;

public class SixenseObjectController : MonoBehaviour {

	public SixenseHands			Hand;
	public Vector3				Sensitivity = new Vector3( 0.01f, 0.01f, 0.01f );
	
	protected bool				m_enabled = false;
	protected Quaternion		m_initialRotation;
	protected Quaternion		m_initialControllerRotation;
	protected Vector3			m_initialPosition;
	protected Vector3			m_baseControllerPosition;
	
	protected bool 				m_bCalibrated = false;
	private Vector3 			m_handOffset;
	public Vector3 ShoulderPosition = new Vector3(0.1f, 1.5f, 0.2f);
	
	//Arduino trigger
	public bool m_enableArduino = true;
		
	// Use this for initialization
	protected virtual void Start() 
	{
		m_initialRotation = this.gameObject.transform.localRotation;
		m_initialPosition = this.gameObject.transform.localPosition;
	}
	
	// Update is called once per frame
	void Update () 
	{
		if ( Hand == SixenseHands.UNKNOWN )
		{
			return;
		}
		
		SixenseInput.Controller controller = SixenseInput.GetController( Hand );
		if ( controller != null && controller.Enabled )  
		{		
			UpdateObject(controller);
		}	
	}
	
	
	void OnGUI()
	{
		if ( !m_enabled )
		{
			GUI.Box( new Rect( Screen.width / 2 - 100, Screen.height - 40, 200, 30 ),  "Press Start To Move/Rotate" );
		}
	}
	
	public void Calibrate(SixenseInput.Controller controller){
		if (!this.m_bCalibrated){
			this.m_handOffset = Vector3.zero;
		    if ((double) controller.Trigger > 0.5)
		    {
			  this.m_bCalibrated = true;
			  Vector3 vector3 = new Vector3(controller.Position.x * Sensitivity.x, controller.Position.y * Sensitivity.y, controller.Position.z * Sensitivity.z);
			  this.m_handOffset = this.ShoulderPosition - vector3;
			  //this.m_playerController.OffsetY = this.m_handOffset.y - this.BaseHandOffsetY;
			  gameObject.transform.localPosition = vector3 + this.m_handOffset + Vector3.up;
		    }
		}
	}
	
	
	
	protected virtual void UpdateObject(  SixenseInput.Controller controller )
	{	
		if ( m_enabled )
		{
			UpdatePosition( controller );
			UpdateRotation( controller );
		}
	}
	
	public void ActivateHand( SixenseInput.Controller controller){
		// enable position and orientation control
		m_enabled = true;
			
		// delta controller position is relative to this point
		m_baseControllerPosition = new Vector3( controller.Position.x * Sensitivity.x,
													controller.Position.y * Sensitivity.y,
													controller.Position.z * Sensitivity.z );
			
		// this is the new start position
		m_initialControllerRotation = controller.Rotation;
		m_initialPosition = this.gameObject.transform.localPosition;
		m_initialRotation = transform.rotation;
	}
	
	
	protected void UpdatePosition( SixenseInput.Controller controller )
	{
		Vector3 controllerPosition = new Vector3( controller.Position.x * Sensitivity.x,
												  controller.Position.y * Sensitivity.y,
												  controller.Position.z * Sensitivity.z );
		
		// distance controller has moved since enabling positional control
		Vector3 vDeltaControllerPos = controllerPosition - m_baseControllerPosition;
		
		// update the localposition of the object
		this.gameObject.transform.localPosition = m_initialPosition + vDeltaControllerPos;
	}
	
	
	protected void UpdateRotation( SixenseInput.Controller controller )
	{
		Quaternion offsetRotation = Quaternion.Inverse(m_initialControllerRotation) * controller.Rotation;
		this.gameObject.transform.localRotation = m_initialRotation * offsetRotation;
	}
}
