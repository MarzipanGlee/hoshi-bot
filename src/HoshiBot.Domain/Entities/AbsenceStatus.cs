namespace HoshiBot.Domain.Entities;

// Confirmed is 0 deliberately: it's the DB column's implicit default, so any row created
// without explicitly setting Status (there shouldn't be any, but this keeps the safe
// case the default one) lands as a normal, visible absence rather than an invisible draft.
// Draft rows are a transient, invisible-until-confirmed holding place for a create/edit
// flow's modal input — see Absence.EditsAbsenceId. They never appear in lists/reports/
// notification-suppression checks and are swept up if abandoned.
public enum AbsenceStatus
{
    Confirmed,
    Draft,
}
