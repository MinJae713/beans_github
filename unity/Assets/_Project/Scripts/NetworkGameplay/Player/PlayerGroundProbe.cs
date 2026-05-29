using Fusion;
using UnityEngine;

namespace Colosseum.Player
{
    public sealed class PlayerGroundProbe
    {
        private static PhysicsMaterial2D s_playerContactMaterial;

        public void EnsurePlayerContactMaterial(Collider2D bodyCollider)
        {
            if (bodyCollider == null)
            {
                return;
            }

            if (s_playerContactMaterial == null)
            {
                s_playerContactMaterial = new PhysicsMaterial2D("PlayerContact")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }

            bodyCollider.sharedMaterial = s_playerContactMaterial;
        }

        public bool IsGroundedOnLevel(Transform groundCheck, Vector2 groundCheckSize, LayerMask groundLayer, NetworkRunner runner)
        {
            if (groundCheck == null)
            {
                return false;
            }

            // A303-306: 벽 옆면 오판정 방지. OverlapBox는 layer만 보고 접촉면 normal을 안 봐서
            // wall layer == ground layer 또는 동일 collider의 측면 접촉을 ground로 잡았다.
            // BoxCast로 바꿔 hit.normal.y > 0.7 (약 45° 이내의 위쪽 면)인 경우만 ground로 인정.
            Vector2 origin = (Vector2)groundCheck.position + Vector2.up * (groundCheckSize.y * 0.5f);
            Vector2 castSize = new Vector2(groundCheckSize.x, 0.02f);
            float castDistance = groundCheckSize.y;

            RaycastHit2D hit;
            if (runner != null)
            {
                hit = runner.GetPhysicsScene2D().BoxCast(origin, castSize, 0f, Vector2.down, castDistance, groundLayer);
            }
            else
            {
                hit = Physics2D.BoxCast(origin, castSize, 0f, Vector2.down, castDistance, groundLayer);
            }

            return hit.collider != null && hit.normal.y > 0.7f;
        }

        /// <summary>
        /// A303-292: 단일 측면 벽 접촉 판정. PlayerController가 좌/우 wallCheck Transform을 따로 들고,
        /// 측별로 이 메서드를 호출해서 부착 후보 여부를 결정한다.
        /// Fusion 결정론을 위해 Runner.GetPhysicsScene2D()를 우선 사용.
        /// </summary>
        public bool IsTouchingWallOnSide(Transform wallCheck, Vector2 wallCheckSize, LayerMask wallLayer, NetworkRunner runner)
        {
            if (wallCheck == null)
            {
                return false;
            }

            if (runner != null)
            {
                return runner.GetPhysicsScene2D().OverlapBox(wallCheck.position, wallCheckSize, 0f, wallLayer) != null;
            }

            return Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, wallLayer) != null;
        }

        public bool IsGroundedOnOtherPlayer(
            PlayerController self,
            Collider2D bodyCollider,
            Rigidbody2D rb,
            float playerGroundSnapDistance,
            float playerGroundPenetrationTolerance,
            float playerGroundHorizontalInset,
            float maxGroundedSupportRiseSpeed)
        {
            if (bodyCollider == null || rb == null)
            {
                return false;
            }

            Bounds myBounds = bodyCollider.bounds;

            foreach (PlayerController other in PlayerController.ActivePlayersInternal)
            {
                if (CanUseOtherPlayerAsGround(
                    self,
                    other,
                    myBounds,
                    rb,
                    playerGroundSnapDistance,
                    playerGroundPenetrationTolerance,
                    playerGroundHorizontalInset,
                    maxGroundedSupportRiseSpeed))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanUseOtherPlayerAsGround(
            PlayerController self,
            PlayerController other,
            Bounds myBounds,
            Rigidbody2D rb,
            float playerGroundSnapDistance,
            float playerGroundPenetrationTolerance,
            float playerGroundHorizontalInset,
            float maxGroundedSupportRiseSpeed)
        {
            if (other == null || other == self || other.BodyCollider == null)
            {
                return false;
            }

            Bounds otherBounds = other.BodyCollider.bounds;
            bool horizontalOverlap =
                myBounds.min.x + playerGroundHorizontalInset < otherBounds.max.x &&
                myBounds.max.x - playerGroundHorizontalInset > otherBounds.min.x;

            if (!horizontalOverlap)
            {
                return false;
            }

            float bottomToTop = myBounds.min.y - otherBounds.max.y;
            bool withinSupportWindow =
                bottomToTop <= playerGroundSnapDistance &&
                bottomToTop >= -playerGroundPenetrationTolerance;

            bool approachingFromAbove =
                myBounds.center.y >= otherBounds.center.y &&
                rb.velocity.y <= maxGroundedSupportRiseSpeed;

            return withinSupportWindow && approachingFromAbove;
        }

        public void ResolvePlayerOverlap(PlayerController self, Collider2D bodyCollider)
        {
            if (bodyCollider == null)
            {
                return;
            }

            foreach (PlayerController other in PlayerController.ActivePlayersInternal)
            {
                if (other == null || other == self || other.BodyCollider == null || other.Rigidbody == null)
                {
                    continue;
                }

                if (self.Object.Id.Raw > other.Object.Id.Raw)
                {
                    continue;
                }

                ResolveOverlapWith(self, other);
            }
        }

        public void ResolveOverlapWith(PlayerController self, PlayerController other)
        {
            Bounds myBounds = self.BodyCollider.bounds;
            Bounds otherBounds = other.BodyCollider.bounds;

            float overlapX = Mathf.Min(myBounds.max.x, otherBounds.max.x) - Mathf.Max(myBounds.min.x, otherBounds.min.x);
            float overlapY = Mathf.Min(myBounds.max.y, otherBounds.max.y) - Mathf.Max(myBounds.min.y, otherBounds.min.y);

            if (overlapX <= 0f || overlapY <= 0f)
            {
                return;
            }

            bool verticalContactDominant = overlapY <= overlapX || Mathf.Abs(myBounds.center.y - otherBounds.center.y) > 0.1f;

            if (verticalContactDominant)
            {
                ResolveVerticalOverlap(self, other, overlapY, myBounds.center.y >= otherBounds.center.y);
                return;
            }

            ResolveHorizontalOverlap(self, other, overlapX, myBounds.center.x >= otherBounds.center.x);
        }

        public void ResolveVerticalOverlap(PlayerController self, PlayerController other, float overlapY, bool isAbove)
        {
            float halfSeparation = (overlapY + self.PlayerOverlapPadding) * 0.5f;
            Vector2 myOffset = isAbove ? Vector2.up * halfSeparation : Vector2.down * halfSeparation;
            Vector2 otherOffset = -myOffset;

            MoveBy(self.Rigidbody, self.transform, myOffset);
            MoveBy(other.Rigidbody, other.transform, otherOffset);

            PlayerController topPlayer = isAbove ? self : other;
            bool groundedOnLevel = IsGroundedOnLevel(
                topPlayer.GroundCheck,
                topPlayer.GroundCheckSize,
                topPlayer.GroundLayer,
                topPlayer.Runner);

            if (!groundedOnLevel)
            {
                ForceDownwardFallAfterPlayerContact(topPlayer.Rigidbody);
            }
        }

        public void ResolveHorizontalOverlap(PlayerController self, PlayerController other, float overlapX, bool isRightSide)
        {
            float halfSeparation = (overlapX + self.PlayerOverlapPadding) * 0.5f;
            Vector2 myOffset = isRightSide ? Vector2.right * halfSeparation : Vector2.left * halfSeparation;
            Vector2 otherOffset = -myOffset;

            MoveBy(self.Rigidbody, self.transform, myOffset);
            MoveBy(other.Rigidbody, other.transform, otherOffset);
            ReduceSidePushVelocity(self, other);
        }

        public void MoveBy(Rigidbody2D rb, Transform tr, Vector2 offset)
        {
            if (offset == Vector2.zero)
            {
                return;
            }

            if (rb != null)
            {
                rb.position += offset;
            }
            else
            {
                tr.position += (Vector3)offset;
            }
        }

        public void ForceDownwardFallAfterPlayerContact(Rigidbody2D rb)
        {
            if (rb == null)
            {
                return;
            }

            if (rb.velocity.y > 0f)
            {
                rb.velocity = new Vector2(rb.velocity.x, 0f);
            }
        }

        public void ReduceSidePushVelocity(PlayerController self, PlayerController other)
        {
            DampenHorizontalVelocity(self.Rigidbody, self.CurrentMoveInputX);
            DampenHorizontalVelocity(other.Rigidbody, other.CurrentMoveInputX);
        }

        public void DampenHorizontalVelocity(Rigidbody2D rb, float moveInputX)
        {
            if (rb == null)
            {
                return;
            }

            float desiredX = Mathf.Abs(moveInputX) > 0.01f ? rb.velocity.x : 0f;
            rb.velocity = new Vector2(desiredX, rb.velocity.y);
        }
    }
}
