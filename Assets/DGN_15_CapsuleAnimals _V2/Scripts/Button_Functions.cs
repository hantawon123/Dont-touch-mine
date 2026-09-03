using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DGN_CapsuleCharacters
{
    public class Button_Functions : MonoBehaviour
    {
        public GameObject[] _Characters;
        public GameObject _Ground;
        public Transform _Placer;
        #region Animations Sets
        public void SetAnimLookAround()
        {
            foreach (GameObject obj in _Characters)
            {
                obj.GetComponent<Animator>().Play("Test_LookingAround");
            }
            _Ground.GetComponent<Rotater>().rotationSpeed = 0f;
            _Ground.GetComponent<Transform>().rotation = Quaternion.Euler(0f, 0f, 0f);
        }
        public void SetAnimWalking()
        {
            foreach (GameObject obj in _Characters)
            {
                obj.GetComponent<Animator>().Play("Test_Walking");
            }
            _Ground.GetComponent<Transform>().rotation = Quaternion.Euler(0f, 0f, 0f);
            _Ground.GetComponent<Rotater>().rotationSpeed = 50f;
        }

        #endregion
        #region Locations Sets
        public void SetPanther()
        {
            _Placer.localPosition = new Vector3(0f, 0f, 0f);
        }
        public void SetHorse()
        {
            _Placer.localPosition = new Vector3(-120f, 0f, 0f);
        }
        public void SetTiger()
        {
            _Placer.localPosition = new Vector3(-20f, 0f, 0f);
        }
        public void SetPolarBear()
        {
            _Placer.localPosition = new Vector3(-40f, 0f, 0f);

        }
        public void SetRaccoon()
        {
            _Placer.localPosition = new Vector3(-60f, 0f, 0f);
        }
        public void SetMonkey()
        {
            _Placer.localPosition = new Vector3(-80f, 0f, 0f);
        }
        public void SetHippo()
        {
            _Placer.localPosition = new Vector3(-220f, 0f, 0f);
        }
        public void SetMoose()
        {
            _Placer.localPosition = new Vector3(-240f, 0f, 0f);
        }
        public void SetZebra()
        {
            _Placer.localPosition = new Vector3(-140f, 0f, 0f);
        }
        public void SetLeopard()
        {
            _Placer.localPosition = new Vector3(-100f, 0f, 0f);
        }
        public void SetBuffalo()
        {
            _Placer.localPosition = new Vector3(-160f, 0f, 0f);
        }
        public void SetDog()
        {
            _Placer.localPosition = new Vector3(-180f, 0f, 0f);
        }
        public void SetElephant()
        {
            _Placer.localPosition = new Vector3(-200f, 0f, 0f);
        }
        public void SetGorilla()
        {
            _Placer.localPosition = new Vector3(-260f, 0f, 0f);
        }
        public void SetRhino()
        {
            _Placer.localPosition = new Vector3(-280f, 0f, 0f);
        }
    }
    #endregion
}
