namespace ForkPlus.Git
{
    public class GitConfig
    {
        public struct Section
        {
            public string Name { get; }
            public string Subsection { get; }
            public Variable[] Variables { get; }

            public Section(string name, string subsection, Variable[] variables)
            {
                Name = name;
                Subsection = subsection;
                Variables = variables;
            }

            public override string ToString()
            {
                string name = Name;
                if (name != null)
                {
                    string subsection = Subsection;
                    if (subsection != null)
                    {
                        return name + "." + subsection;
                    }
                    return name;
                }
                return "<empty>";
            }

            public bool SectionEquals(Section other)
            {
                if (Name == other.Name && Subsection == other.Subsection)
                {
                    return VariablesAreEqual(Variables, other.Variables);
                }
                return false;
            }

            private static bool VariablesAreEqual(Variable[] current, Variable[] old)
            {
                if (current.Length != old.Length)
                {
                    return false;
                }
                for (int i = 0; i < current.Length; i++)
                {
                    if (!current[i].VariableEquals(old[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public struct Variable
        {
            public string Name { get; }
            public string Value { get; }

            public Variable(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public override string ToString()
            {
                return Name + " = " + Value;
            }

            public bool VariableEquals(Variable other)
            {
                if (Name == other.Name)
                {
                    return Value == other.Value;
                }
                return false;
            }
        }

        public Section[] Sections { get; }

        public GitConfig(Section[] sections)
        {
            Sections = sections;
        }

        [Null]
        public string GetString(string section, [Null] string subsection, string key)
        {
            Section[] sections = Sections;
            for (int i = 0; i < sections.Length; i++)
            {
                Section section2 = sections[i];
                if (section2.Name != section && section2.Subsection != subsection)
                {
                    continue;
                }
                Variable[] variables = section2.Variables;
                for (int j = 0; j < variables.Length; j++)
                {
                    Variable variable = variables[j];
                    if (variable.Name == key)
                    {
                        return variable.Value;
                    }
                }
            }
            return null;
        }

        public bool GitConfigEquals(GitConfig other)
        {
            return GitConfigSectionsAreEqual(Sections, other.Sections);
        }

        private static bool GitConfigSectionsAreEqual(Section[] current, Section[] old)
        {
            if (current.Length != old.Length)
            {
                return false;
            }
            for (int i = 0; i < current.Length; i++)
            {
                if (!current[i].SectionEquals(old[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
