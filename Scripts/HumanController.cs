﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HumanController : MonoBehaviour
{
    private float mSpeed = 0;
    private bool sprinting = false;

    [Header("Speeds")]
    [SerializeField] float turnSpeed = 0.1f;
    [SerializeField] float moveSpeed = 1.0f;
    [SerializeField] float sprintSpeed = 3.0f;

    [Header("Accelerations")]
    [SerializeField] float turnAcceleration = 2.0f;
    [SerializeField] float moveAcceleration = 2.0f;

    [Header("Distances to target")]
    [SerializeField] float minDistToTarget = 0.2f;
    [SerializeField] float maxDistToTarget = 3.0f;

    [Header("Max angle to target")]
    [SerializeField] float maxAngToTarget = 0.2f;

    [Header("Step distances")]
    [SerializeField] public float wantStepAtDistance = 0.45f;
    [SerializeField] public float sprintStepDistance = 0.5f;

    // Fraction of the max distance from home we want to overshoot by
    [Header("Step overshoots")]
    [SerializeField] float stepOvershootFraction = 0.44f;
    [SerializeField] float sprintStepOverShootFraction = 0.49f;

    // How long a step takes to complete
    [Header("Move durations")]
    [SerializeField] float moveDuration = 0.3f;
    [SerializeField] float sprintMoveDuration = 0.15f;
    

    float stepAtDistance = 0.45f;
    float moveDur = 0.3f;
    float stepOvershoot = 0.44f;

    Vector3 currentVelocity;
    float currentAngularVelocity;

    //Target that will be tracked
    [Header("Follow target")]
    [SerializeField] Transform Target;

    // Reference to head of character
    [Header("Head object reference")]
    [SerializeField] Transform headBone;

    //Refernce to body of character
    [Header("Body object reference")]
    [SerializeField] Transform Body;

    [Header("Head related variables")]
    [SerializeField] float headMaxTurnAngle;
    [SerializeField] float headTrackingSpeed;
    // Start is called before the first frame update

    [Header("Legstepper references")]
    [SerializeField] LegStepper LeftLegStepper;
    [SerializeField] LegStepper RightLegStepper;

    float lerpValue = 0;
    
    IEnumerator LegUpdateCoroutine()
    {
        // Run continuously
        while (true)
        {
            // Try moving one leg at a time
            do
            {
                if (!LeftLegStepper.Moving)
                    RightLegStepper.TryMove(stepAtDistance, moveDur, stepOvershoot);

                if (!RightLegStepper.Moving)
                    LeftLegStepper.TryMove(stepAtDistance, moveDur, stepOvershoot);

               
                // Wait a frame
                yield return null;

                // Stay in this loop while either leg is moving.
                // If only one leg in the pair is moving, the calls to TryMove() will let
                // the other leg move if it wants to.
            } while (LeftLegStepper.Moving ||RightLegStepper.Moving);

        }
    }



    void Awake()
    {

        StartCoroutine(LegUpdateCoroutine());
    }
    // Head motion helper function
    void HeadMotionUpdate()
    {
        //Store the current head rotation
        Quaternion currentLocalRotation = headBone.localRotation;

        //Reset the head rotation so our world to local space transformation will use the head zero-rotation.
        headBone.localRotation = Quaternion.identity;


        Vector3 targetWorldLookDir = Target.position - headBone.position;
        Vector3 targetLocalLookDir = headBone.InverseTransformDirection(targetWorldLookDir);

        // Apply angle limit
        targetLocalLookDir = Vector3.RotateTowards(
          Vector3.forward,
          targetLocalLookDir,
          Mathf.Deg2Rad * headMaxTurnAngle, // Multiply by Mathf.Deg2Rad here to convert degrees to radians
          0 // We don't care about the length here, so we leave it at zero
        );

        // Get the local rotation by using LookRotation on a local directional vector
        Quaternion targetLocalRotation = Quaternion.LookRotation(targetLocalLookDir, Vector3.up);

        // Apply smoothing
        headBone.localRotation = Quaternion.Slerp(
          currentLocalRotation,
          targetLocalRotation,
          1 - Mathf.Exp(-headTrackingSpeed * Time.deltaTime)
        );

    }
    // Body motion helper function
    void RootMotionUpdate(float mSpeed)
    {
   
        Quaternion bodyLocalRotation = Body.localRotation;
        // Get the direction toward our target
        Vector3 towardTarget = Target.position - Body.position;
  
        // Vector toward target on the local XZ plane
        Vector3 towardTargetProjected = Vector3.ProjectOnPlane(towardTarget, Body.up);
        // Get the angle from the body's forward direction to the direction towards our target
        // Here we get the signed angle around the up vector so we know which direction to turn in
        float angToTarget = Vector3.SignedAngle(Body.forward, towardTargetProjected, Body.up);
        float targetAngularVelocity = 0;

        // If we are within the max angle (i.e. approximately facing the target)
        // leave the target angular velocity at zero
        if (Mathf.Abs(angToTarget) > maxAngToTarget)
        {
  
            // Angles in Unity are clockwise, so a positive angle here means to our right
            if (angToTarget > 0)
            {
                targetAngularVelocity = turnSpeed;
            }
            // Invert angular speed if target is to our left
            else
            {
                targetAngularVelocity = -turnSpeed;
            }
        }

 
        // Use smoothing function to gradually change the velocity
        currentAngularVelocity = Mathf.Lerp(
          currentAngularVelocity,
          targetAngularVelocity,
          1 - Mathf.Exp(-turnAcceleration * Time.deltaTime)
        );

        // Rotate the transform around the Y axis in world space, 
        // making sure to multiply by delta time to get a consistent angular velocity
        Body.Rotate(0, currentAngularVelocity, 0, Space.World);

        Vector3 targetVelocity = Vector3.zero;

        // Don't move if we're facing away from the target, just rotate in place
        if (Mathf.Abs(angToTarget) < 90)
        {
            float distToTarget = Vector3.Distance(Body.position, Target.position);

            // If we're too far away, approach the target
            if (distToTarget > maxDistToTarget)
            {
                targetVelocity = mSpeed * towardTargetProjected.normalized;
            }
            // If we're too close, reverse the direction and move away
            else if (distToTarget < minDistToTarget)
            {
                targetVelocity = mSpeed * -towardTargetProjected.normalized;
            }
        }

        currentVelocity = Vector3.Lerp(
          currentVelocity,
          targetVelocity,
          1 - Mathf.Exp(-moveAcceleration * Time.deltaTime)
        );

        // Apply the velocity
        Body.position += currentVelocity * Time.deltaTime;
        lerpValue += Time.deltaTime / 1.0f;

    }

   

    void Update()
    {
        // Alter variabler depending on if shift is held for "sprint"
        mSpeed = (Input.GetButton("Fire3") ? sprintSpeed : moveSpeed);
        stepAtDistance = (Input.GetButton("Fire3") ? sprintStepDistance : wantStepAtDistance);
        moveDur = (Input.GetButton("Fire3") ? sprintMoveDuration : moveDuration);
        stepOvershoot = (Input.GetButton("Fire3") ? sprintStepOverShootFraction : stepOvershootFraction);

        //Update head and body movement continuously
        RootMotionUpdate(mSpeed);
        HeadMotionUpdate();
      
    }
}

