namespace DinamicScreenSound;

internal sealed record OutputChoice(string Label, bool Assigned, string? EndpointId)
{
    public override string ToString() => Label;

    internal static void Populate(ComboBox selection, IReadOnlyList<AudioEndpoint> outputs, string unassignedLabel,
        bool hasAssignment, string? endpointId)
    {
        selection.Items.Clear();
        selection.Items.Add(new OutputChoice(unassignedLabel, false, null));
        selection.Items.Add(new OutputChoice("Windows default output", true, null));
        foreach (AudioEndpoint output in outputs.OrderBy(output => output.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            selection.Items.Add(new OutputChoice(output.Name, true, output.Id));
        }

        if (!hasAssignment)
        {
            selection.SelectedIndex = 0;
            return;
        }

        OutputChoice? selected = selection.Items.Cast<OutputChoice>().FirstOrDefault(choice =>
            choice.Assigned && string.Equals(choice.EndpointId, endpointId, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            selected = new OutputChoice("Unavailable saved output", true, endpointId);
            selection.Items.Add(selected);
        }

        selection.SelectedItem = selected;
    }
}
