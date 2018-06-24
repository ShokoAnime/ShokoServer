namespace Shoko.Server.API.v2.Models.common
{
    public class Role
    {
        public string character { get; set; }
        public string character_image { get; set; }
        public string character_description { get; set; }
        public string staff { get; set; }
        public string staff_image { get; set; }
        public string staff_description { get; set; }
        public string role { get; set; }
        public string type { get; set; }

        protected bool Equals(Role other)
        {
            return string.Equals(character, other.character) && string.Equals(staff, other.staff);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Role) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((character != null ? character.GetHashCode() : 0) * 397) ^ (staff != null ? staff.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Role left, Role right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Role left, Role right)
        {
            return !Equals(left, right);
        }
    }
}