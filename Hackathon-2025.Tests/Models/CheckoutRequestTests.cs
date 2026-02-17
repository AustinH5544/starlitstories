using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models
{
    [TestClass]
    public class CheckoutRequestTests
    {
        [TestMethod]
        public void CanAssignMembershipEnumValue()
        {
            // Arrange
            var request = new CheckoutRequest
            {
                Membership = MembershipPlan.Premium
            };

            // Assert
            Assert.AreEqual(MembershipPlan.Premium, request.Membership);
        }

        [TestMethod]
        public void RequiredProperty_Membership_MustBeSet()
        {
            // Act & Assert
            // Since `Membership` is marked as `required`, omitting it will cause a compile error.
            // So instead, we just ensure that a valid enum can be set and read correctly.
            var request = new CheckoutRequest { Membership = MembershipPlan.Free };
            Assert.AreEqual(MembershipPlan.Free, request.Membership);
        }
    }
}
