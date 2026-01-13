using System;
using System.Collections;
using UnityEngine;

namespace PlayKit_SDK.Art
{
    public class LoadingSpinner : MonoBehaviour
    {
        [Tooltip("The rotating spinner element inside the loading modal.")]
        [SerializeField] private RectTransform spinner;
        private Coroutine _spinCoroutine;

        private void OnEnable()
        {
            if (_spinCoroutine == null && spinner != null)
            {
                _spinCoroutine = StartCoroutine(Spin());
            }
        }

        private void OnDisable()
        {
            if (_spinCoroutine != null)
            {
                StopCoroutine(_spinCoroutine);
                _spinCoroutine = null;
            }
        }

        private IEnumerator Spin()
        {
            while (true)
            {
                spinner.Rotate(0f, 0f, -180f * Time.deltaTime);
                yield return null;
            }
        }
    }
}