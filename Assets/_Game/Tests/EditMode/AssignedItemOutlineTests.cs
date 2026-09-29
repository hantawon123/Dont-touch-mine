using Game.Client.Interactions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class AssignedItemOutlineTests
    {
        [Test]
        public void Carryable_ShowsOutlineOnlyWhileLocallyAssigned()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();

            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                item.SetAssignedHighlight(true);

                var outline = item.GetComponent<AssignedItemOutline>();
                Assert.That(outline, Is.Not.Null);
                Assert.That(outline.IsVisible, Is.True);

                item.SetAssignedHighlight(false);
                Assert.That(outline.IsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void Carryable_ShowsOrangeFocusOutlineWhileAimed()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();

            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                item.SetAimed(true, 1f);

                var outline = item.GetComponent<InteractableFocusOutline>();
                Assert.That(outline, Is.Not.Null);
                Assert.That(outline.IsVisible, Is.True);
                Assert.That(outline.Color, Is.EqualTo(new Color(1f, 154f / 255f, 106f / 255f, 1f)));
                Assert.That(outline.PixelWidth, Is.EqualTo(2f));
                Assert.That(outline.SeeThrough, Is.True, "집는 물건은 옆 물건에 가려져도 보이는 실루엣을 쓴다.");
                Assert.That(FindChild(itemObject.transform, InteractableFocusOutline.MaskChildName), Is.Not.Null);
                Assert.That(FindChild(itemObject.transform, InteractableFocusOutline.OutlineChildName), Is.Not.Null);

                item.SetAimed(false, 1f);
                Assert.That(outline.IsVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void FocusOutline_SeeThrough_DrawsStencilMaskThenDepthIgnoringHull()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                var outline = itemObject.AddComponent<InteractableFocusOutline>();
                outline.SeeThrough = true;
                outline.SetVisible(true);

                var mask = FindChild(itemObject.transform, InteractableFocusOutline.MaskChildName);
                var hull = FindChild(itemObject.transform, InteractableFocusOutline.OutlineChildName);
                Assert.That(mask, Is.Not.Null, "1단계 스텐실 마스크 복사본이 있어야 한다.");
                Assert.That(hull, Is.Not.Null);

                var maskMaterial = mask.GetComponent<Renderer>().sharedMaterial;
                Assert.That(maskMaterial.GetFloat("_ColorMask"), Is.EqualTo(0f), "마스크는 색을 쓰지 않는다.");
                Assert.That(maskMaterial.GetFloat("_ZTest"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
                Assert.That(maskMaterial.GetFloat("_StencilPass"), Is.EqualTo((float)UnityEngine.Rendering.StencilOp.Replace));
                Assert.That(maskMaterial.GetFloat("_StencilRef"), Is.EqualTo((float)InteractableFocusOutline.StencilBit));

                var hullMaterial = hull.GetComponent<Renderer>().sharedMaterial;
                Assert.That(hullMaterial.GetFloat("_ZTest"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
                Assert.That(hullMaterial.GetFloat("_StencilComp"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.NotEqual));
                Assert.That(hullMaterial.GetFloat("_Cull"), Is.EqualTo((float)UnityEngine.Rendering.CullMode.Front));
                Assert.That(hullMaterial.renderQueue, Is.GreaterThan(maskMaterial.renderQueue),
                    "마스크가 먼저 그려져야 껍질이 그 바깥에만 남는다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void FocusOutline_Default_KeepsDepthTestedHullWithoutMask()
        {
            var propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                var outline = propObject.AddComponent<InteractableFocusOutline>();
                outline.SetVisible(true);

                Assert.That(outline.SeeThrough, Is.False, "상시 표시 설치물(계획판)은 기존 방식 그대로여야 한다.");
                Assert.That(FindChild(propObject.transform, InteractableFocusOutline.MaskChildName), Is.Null);
                var hull = FindChild(propObject.transform, InteractableFocusOutline.OutlineChildName);
                Assert.That(hull, Is.Not.Null);
                var hullMaterial = hull.GetComponent<Renderer>().sharedMaterial;
                Assert.That(hullMaterial.GetFloat("_ZTest"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.LessEqual));
                Assert.That(hullMaterial.GetFloat("_StencilComp"), Is.EqualTo((float)UnityEngine.Rendering.CompareFunction.Always));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(propObject);
            }
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        [Test]
        public void Carryable_HidesAssignedOutlineWhileAimed()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.AddComponent<Rigidbody>();

            try
            {
                var item = itemObject.AddComponent<CarryableItem>();
                item.SetAssignedHighlight(true);
                item.SetAimed(true, 1f);

                Assert.That(item.GetComponent<AssignedItemOutline>().IsVisible, Is.False);
                Assert.That(item.GetComponent<InteractableFocusOutline>().IsVisible, Is.True);

                item.SetAimed(false, 1f);
                Assert.That(item.GetComponent<AssignedItemOutline>().IsVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }
    }
}
