namespace Soenneker.Cosmos.Repository
{
    internal static class CosmosRepositoryStatics
    {
        public static readonly string[] IdParameterNames =
        [
            "@i0","@i1","@i2","@i3","@i4","@i5","@i6","@i7","@i8","@i9",
            "@i10","@i11","@i12","@i13","@i14","@i15","@i16","@i17","@i18","@i19",
            "@i20","@i21","@i22","@i23","@i24","@i25","@i26","@i27","@i28","@i29",
            "@i30","@i31","@i32","@i33","@i34","@i35","@i36","@i37","@i38","@i39",
            "@i40","@i41","@i42","@i43","@i44","@i45","@i46","@i47","@i48","@i49"
        ];

        public static readonly string[] IdInQueryTexts = BuildIdInQueryTexts();

        private static string[] BuildIdInQueryTexts()
        {
            var results = new string[IdParameterNames.Length];
            var query = new System.Text.StringBuilder(32 + IdParameterNames.Length * 6);
            query.Append("SELECT * FROM c WHERE c.id IN (");

            for (var i = 0; i < IdParameterNames.Length; i++)
            {
                if (i != 0)
                    query.Append(',');

                query.Append(IdParameterNames[i]);
                query.Append(')');
                results[i] = query.ToString();
                query.Length--;
            }

            return results;
        }
    }
}
