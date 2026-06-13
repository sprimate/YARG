using UnityEngine;
using YARG.Menu.ListMenu;

namespace YARG
{
    public class TourListViewType : BaseViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        public TourData TourData { get; private set; }
        public TourListViewType(TourData tourData)
        {
            TourData = tourData;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(TourData.TourName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return string.Empty;
        }
    }
}
