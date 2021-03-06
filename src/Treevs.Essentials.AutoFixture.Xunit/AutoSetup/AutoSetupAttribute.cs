﻿namespace Treevs.Essentials.AutoFixture.Xunit.AutoSetup
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Xunit;
    using Treevs.Essentials.AutoFixture.Xunit.AutoSetup.ActionProviders;

    /// <summary>
    /// Enables an XUnit Theory test method to have its parameters generated by Autofixture, via an <see cref="IFixture"/>
    /// instance that can be configured with static setup methods or properties.  The <see cref="IFixture"/> instance can also
    /// be requested/injected into the test method.
    /// 
    /// A method or property called 'AutoSetup' will be used implicitly on the test fixture if it exists and returns <see cref="Action{T}"/> of <see cref="IFixture"/>/>
    /// </summary>
    public class AutoSetupAttribute : AutoDataAttribute
    {
        private const string DefaultFixtureSetupName = "AutoSetup";

        private const string AutoSetupExternalSourceFieldName = "AutoSetupSource";

        private readonly string[] _fixtureSetups;

        private readonly Type _classSource;

        private readonly IEnumerable<ISetupActionsProvider> _setupActionsProviders;

        /// <summary>
        /// Defines that this test will have its parameter values generated by an Autofixture <see cref="IFixture"/> instance, which can be
        /// configured by static methods on this fixture class.
        /// </summary>
        /// <param name="fixtureSetups">The names of the public static methods that return <see cref="Action{T}"/> of <see cref="IFixture"/>. 'AutoSetup' is inserted as the first name if it is not specified.</param>
        public AutoSetupAttribute(params string[] fixtureSetups) :
            this(null, fixtureSetups)
        {
        }

        /// <summary>
        /// Defines that this test will have its parameter values generated by an Autofixture <see cref="IFixture"/> instance, which can be
        /// configured by public static methods on a specified class.
        /// </summary>
        /// <param name="externalClassSource">The class containing the public static methods to configure the <see cref="IFixture"/> instance.</param>
        /// <param name="fixtureSetups">The names of the public static methods that return <see cref="Action{T}"/> of <see cref="IFixture"/>. 'AutoSetup' is inserted as the first name if it is not specified.</param>
        public AutoSetupAttribute(Type externalClassSource, params string[] fixtureSetups) :
            this(
               new List<ISetupActionsProvider>
                   {
                       new StaticMethodSetupActionsProvider(), 
                       new StaticPropertySetupActionsProvider(),
                       new PlainMethodSetupActionsProvider()
                   },
               externalClassSource,
               fixtureSetups)
        {
        }

        protected AutoSetupAttribute(
            IEnumerable<ISetupActionsProvider> setupActionsProviders,
            Type externalClassSource,
            params string[] fixtureSetups)
            : base(new Fixture())
        {
            if (!fixtureSetups.Any())
            {
                fixtureSetups = new[] { DefaultFixtureSetupName };
            }

            if (!fixtureSetups.Contains(DefaultFixtureSetupName))
            {
                fixtureSetups = new[] { DefaultFixtureSetupName }.Concat(fixtureSetups).ToArray();
            }

            _fixtureSetups = fixtureSetups;
            Fixture.Register(() => Fixture); // allows tests to request the fixture instance
            _setupActionsProviders = setupActionsProviders;
            _classSource = externalClassSource;
        }

        public override IEnumerable<object[]> GetData(MethodInfo methodUnderTest, Type[] parameterTypes)
        {
            var finalClassSourceType = 
                _classSource ??
                SetupActionsServices.GetActionSourceTypeField(methodUnderTest.ReflectedType, AutoSetupExternalSourceFieldName) ??
                SetupActionsServices.GetActionSourceTypeProperty(methodUnderTest.ReflectedType, AutoSetupExternalSourceFieldName) ?? 
                methodUnderTest.ReflectedType;

            foreach (var action in this.GetSetups(finalClassSourceType))
            {
                action(Fixture);
            }

            return base.GetData(methodUnderTest, parameterTypes);
        }

        public IEnumerable<Action<IFixture>> GetSetups(Type functionSourceType)
        {
            var setupActions = new List<Action<IFixture>>();

            foreach (var fixtureSetup in _fixtureSetups.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                var setups = _setupActionsProviders
                    .SelectMany(p => p.GetSetupActions(functionSourceType, fixtureSetup))
                    .ToList();

                if (!setups.Any() && !fixtureSetup.Equals(DefaultFixtureSetupName, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentOutOfRangeException(fixtureSetup, "No static property, method or field could be found on the test fixture with the name " + fixtureSetup);
                }

                setupActions.AddRange(setups);
            }

            return setupActions;
        }
    }
}
