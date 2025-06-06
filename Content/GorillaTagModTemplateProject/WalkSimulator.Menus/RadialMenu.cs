<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;

namespace WalkSimulator.Menus
{
    public class RadialMenu : MonoBehaviour
    {
        [Serializable]
        public struct Icon
        {
            public Image image;
            public Vector2 direction;
            public AnimatorBase animator;
        }

        public List<Icon> icons;

        private AnimatorBase selectedAnimator;
        private bool cursorWasLocked;
        private bool wasTurning;

        private void Awake()
        {
            icons = new List<Icon>();
            Transform iconsTransform = transform.Find("Icons");

            if (iconsTransform == null)
            {
                Debug.LogError("Icons transform not found.");
                return;
            }

            Image walkImage = iconsTransform.Find("Walk").GetComponent<Image>();
            Image poseImage = iconsTransform.Find("Pose").GetComponent<Image>();
            Image flyImage = iconsTransform.Find("Fly").GetComponent<Image>();

            if (walkImage == null || poseImage == null || flyImage == null)
            {
                Debug.LogError("One or more icon images not found.");
                return;
            }

            icons.Add(new Icon
            {
                image = walkImage,
                direction = Vector2.up,
                animator = Plugin.Instance.walkAnimator
            });
            icons.Add(new Icon
            {
                image = poseImage,
                direction = Vector2.down,
                animator = Plugin.Instance.handAnimator
            });
            icons.Add(new Icon
            {
                image = flyImage,
                direction = Vector2.right,
                animator = Plugin.Instance.flyAnimator
            });
        }

        private void Update()
        {
            Vector2 mousePosition = Mouse.current.position.value;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if ((mousePosition - screenCenter).magnitude < Screen.width / 20f)
            {
                return; // Mouse is too close to the center
            }

            Icon closestIcon = default;
            float minDistance = float.PositiveInfinity;

            foreach (Icon icon in icons)
            {
                float distance = Vector2.Distance(mousePosition - screenCenter, icon.direction);
                if (distance < minDistance)
                {
                    closestIcon = icon;
                    minDistance = distance;
                }
            }

            selectedAnimator = closestIcon.animator;

            foreach (Icon icon in icons)
            {
                bool isSelected = icon.Equals(closestIcon);
                icon.image.color = isSelected ? Color.white : Color.gray;
                icon.image.transform.localScale = Vector3.one * (isSelected ? 1.5f : 1f);
            }
        }

        private void OnEnable()
        {
            if (HeadDriver.Instance == null)
            {
                Debug.LogWarning("HeadDriver.Instance is null, RadialMenu may not function correctly.");
                enabled = false;
                return;
            }

            cursorWasLocked = HeadDriver.Instance.LockCursor;
            wasTurning = HeadDriver.Instance.turn;
            HeadDriver.Instance.LockCursor = false;
            HeadDriver.Instance.turn = false;
        }

        private void OnDisable()
        {
            Logging.Debug("RadialMenu disabled");

            if (HeadDriver.Instance == null)
            {
                Debug.LogWarning("HeadDriver.Instance is null, RadialMenu may not function correctly.");
                return;
            }

            HeadDriver.Instance.LockCursor = cursorWasLocked;
            HeadDriver.Instance.turn = wasTurning;
            Rig.Instance.Animator = selectedAnimator;
            Logging.Debug("--Finished");
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WalkSimulator.Animators;
using WalkSimulator.Rigging;

namespace WalkSimulator.Menus
{
    public class RadialMenu : MonoBehaviour
    {
        [Serializable]
        public struct Icon
        {
            public Image image;
            public Vector2 direction;
            public AnimatorBase animator;
        }

        public List<Icon> icons;

        private AnimatorBase selectedAnimator;
        private bool cursorWasLocked;
        private bool wasTurning;

        private void Awake()
        {
            icons = new List<Icon>();
            Transform iconsTransform = transform.Find("Icons");

            if (iconsTransform == null)
            {
                Debug.LogError("Icons transform not found.");
                return;
            }

            Image walkImage = iconsTransform.Find("Walk").GetComponent<Image>();
            Image poseImage = iconsTransform.Find("Pose").GetComponent<Image>();
            Image flyImage = iconsTransform.Find("Fly").GetComponent<Image>();

            if (walkImage == null || poseImage == null || flyImage == null)
            {
                Debug.LogError("One or more icon images not found.");
                return;
            }

            icons.Add(new Icon
            {
                image = walkImage,
                direction = Vector2.up,
                animator = Plugin.Instance.walkAnimator
            });
            icons.Add(new Icon
            {
                image = poseImage,
                direction = Vector2.down,
                animator = Plugin.Instance.handAnimator
            });
            icons.Add(new Icon
            {
                image = flyImage,
                direction = Vector2.right,
                animator = Plugin.Instance.flyAnimator
            });
        }

        private void Update()
        {
            Vector2 mousePosition = Mouse.current.position.value;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if ((mousePosition - screenCenter).magnitude < Screen.width / 20f)
            {
                return; // Mouse is too close to the center
            }

            Icon closestIcon = default;
            float minDistance = float.PositiveInfinity;

            foreach (Icon icon in icons)
            {
                float distance = Vector2.Distance(mousePosition - screenCenter, icon.direction);
                if (distance < minDistance)
                {
                    closestIcon = icon;
                    minDistance = distance;
                }
            }

            selectedAnimator = closestIcon.animator;

            foreach (Icon icon in icons)
            {
                bool isSelected = icon.Equals(closestIcon);
                icon.image.color = isSelected ? Color.white : Color.gray;
                icon.image.transform.localScale = Vector3.one * (isSelected ? 1.5f : 1f);
            }
        }

        private void OnEnable()
        {
            if (HeadDriver.Instance == null)
            {
                Debug.LogWarning("HeadDriver.Instance is null, RadialMenu may not function correctly.");
                enabled = false;
                return;
            }

            cursorWasLocked = HeadDriver.Instance.LockCursor;
            wasTurning = HeadDriver.Instance.turn;
            HeadDriver.Instance.LockCursor = false;
            HeadDriver.Instance.turn = false;
        }

        private void OnDisable()
        {
            Logging.Debug("RadialMenu disabled");

            if (HeadDriver.Instance == null)
            {
                Debug.LogWarning("HeadDriver.Instance is null, RadialMenu may not function correctly.");
                return;
            }

            HeadDriver.Instance.LockCursor = cursorWasLocked;
            HeadDriver.Instance.turn = wasTurning;
            Rig.Instance.Animator = selectedAnimator;
            Logging.Debug("--Finished");
        }
    }
}
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
