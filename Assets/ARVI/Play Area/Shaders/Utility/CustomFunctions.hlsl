void CheckRowAndColumnEven_float(float row, float column, out bool result)
{
    if (isnan(row) || isnan(column))
    {
        result = false;
    }

    result = (fmod(row, 2.0) == 0.0) && (fmod(column, 2.0) == 0.0);
}

void CheckValueEven_float(float value, out bool result)
{
    if (isnan(value))
    {
        result = false;
    }

    result = (fmod(value, 2.0) == 0.0);
}