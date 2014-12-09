﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;

namespace SiliconStudio.Paradox.Effects.Images
{
    public class ColorTransformGroup : ImageEffect, IImageEffectParameterKeyDependencies
    {
        private readonly ParameterCollection transformsParameters;

        private readonly ImageEffectShader transformGroupEffect;

        private readonly Dictionary<ParameterCompositeKey, ParameterKey> compositeKeys;

        private readonly List<ColorTransform> transforms;

        private readonly List<ColorTransform> collectTransforms;

        private readonly List<ColorTransform> enabledTransforms;

        private readonly GammaTransform gammaTransform;

        private readonly ColorTransformContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorTransformGroup"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="colorTransformGroupEffect">The color transform group effect.</param>
        public ColorTransformGroup(ImageEffectContext context, string colorTransformGroupEffect = "ColorTransformGroupEffect")
            : base(context, colorTransformGroupEffect)
        {
            compositeKeys = new Dictionary<ParameterCompositeKey, ParameterKey>();
            transforms = new List<ColorTransform>();
            enabledTransforms = new List<ColorTransform>();
            collectTransforms = new List<ColorTransform>();

            transformsParameters = new ParameterCollection();

            gammaTransform = new GammaTransform();

            transformGroupEffect = new ImageEffectShader(context, colorTransformGroupEffect, Parameters);

            // we are adding parameter collections after as transform parameters should override previous parameters
            transformGroupEffect.ParameterCollections.Add(transformsParameters);

            this.context = new ColorTransformContext(this);
        }

        /// <summary>
        /// Gets the color transforms.
        /// </summary>
        /// <value>The transforms.</value>
        public List<ColorTransform> Transforms
        {
            get
            {
                return transforms;
            }
        }

        /// <summary>
        /// Gets the gamma transform that is applied after all <see cref="Transforms"/>
        /// </summary>
        /// <value>The gamma transform.</value>
        public GammaTransform GammaTransform
        {
            get
            {
                return gammaTransform;
            }
        }

        protected override void DrawCore()
        {
            var output = GetOutput(0);
            if (output == null)
            {
                return;
            }

            // Collect all transform parameters
            CollectTransformsParameters();

            for (int i = 0; i < context.Inputs.Count; i++)
            {
                transformGroupEffect.SetInput(i, context.Inputs[i]);
            }
            transformGroupEffect.SetOutput(output);
            transformGroupEffect.Draw(Name);
        }

        protected virtual void CollectPreTransforms()
        {
        }


        protected virtual void CollectPostTransforms()
        {
            AddTemporaryTransform(gammaTransform);
        }

        void IImageEffectParameterKeyDependencies.FillParameterKeyDependencies(HashSet<ParameterKey> dependencies)
        {
            foreach (var transform in CollectTransforms())
            {
                var effectDepdencies = transform as IImageEffectParameterKeyDependencies;
                if (effectDepdencies != null)
                {
                    effectDepdencies.FillParameterKeyDependencies(dependencies);
                }
            }
        }

        protected void AddTemporaryTransform(ColorTransform transform)
        {
            if (transform == null) throw new ArgumentNullException("transform");
            if (transform.Shader == null) throw new ArgumentOutOfRangeException("transform", "Transform parameter must have a Shader not null");
            collectTransforms.Add(transform);
        }

        private List<ColorTransform> CollectTransforms()
        {
            collectTransforms.Clear();
            CollectPreTransforms();
            collectTransforms.AddRange(transforms);
            CollectPostTransforms();

            // Copy all parameters from ColorTransform to effect parameters
            enabledTransforms.Clear();
            enabledTransforms.AddRange(collectTransforms);
            return enabledTransforms;
        }

        private void CollectTransformsParameters()
        {
            context.Inputs.Clear();
            for (int i = 0; i < InputCount; i++)
            {
                context.Inputs.Add(GetInput(i));
            }

            // Grab all color transforms
            CollectTransforms();

            transformsParameters.Clear();
            for (int i = 0; i < enabledTransforms.Count; i++)
            {
                var transform = enabledTransforms[i];
                // Update parameters only if transform is active
                //if (transform.Enable)
                {
                    transform.UpdateParameters(context);

                    // Copy transform parameters back to the composition with the current index
                    var sourceParameters = transform.Parameters;
                    foreach (var parameterValue in sourceParameters )
                    {
                        var key = GetComposedKey(parameterValue.Key, i);
                        sourceParameters.CopySharedTo(parameterValue.Key, key, transformsParameters);
                    }
                }
            }

            // NOTE: This is very important to reset the transforms here, as pre-caching by DynamicEffectCompiler is done on parameters changes
            // and as we have a list here, modifying a list doesn't trigger a change for the specified key
            Parameters.Set(ColorTransformGroupKeys.Transforms, enabledTransforms);
        }

        private ParameterKey GetComposedKey(ParameterKey key, int transformIndex)
        {
            var compositeKey = new ParameterCompositeKey(key, transformIndex);

            ParameterKey rawCompositeKey;
            if (!compositeKeys.TryGetValue(compositeKey, out rawCompositeKey))
            {
                rawCompositeKey = ParameterKeys.FindByName(string.Format("{0}.Transforms[{1}]", key.Name, transformIndex));
                compositeKeys.Add(compositeKey, rawCompositeKey);
            }
            return rawCompositeKey;
        }

        /// <summary>
        /// An internal key to cache {Key,TransformIndex} => CompositeKey
        /// </summary>
        private struct ParameterCompositeKey : IEquatable<ParameterCompositeKey>
        {
            private readonly ParameterKey key;

            private readonly int index;

            /// <summary>
            /// Initializes a new instance of the <see cref="ParameterCompositeKey"/> struct.
            /// </summary>
            /// <param name="key">The key.</param>
            /// <param name="transformIndex">Index of the transform.</param>
            public ParameterCompositeKey(ParameterKey key, int transformIndex)
            {
                if (key == null) throw new ArgumentNullException("key");
                this.key = key;
                index = transformIndex;
            }

            public bool Equals(ParameterCompositeKey other)
            {
                return key.Equals(other.key) && index == other.index;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is ParameterCompositeKey && Equals((ParameterCompositeKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (key.GetHashCode() * 397) ^ index;
                }
            }
        }
    }
}